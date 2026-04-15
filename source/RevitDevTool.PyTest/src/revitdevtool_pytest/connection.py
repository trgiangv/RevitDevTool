"""Bridge lifecycle — discover, connect, lease, launch, retry.

Stateless module: every function receives what it needs as parameters.
No ``global`` statements, no module-level mutable state.
"""

from __future__ import annotations

import ctypes
import ctypes.wintypes
import logging
import time
from dataclasses import dataclass
from typing import TYPE_CHECKING

import pytest

from .bridge import RevitBridge
from .constants import (
    EXIT_CODE_CONFIG_ERROR,
    PLUGIN_NAME,
)
from .discovery import (
    find_revit_path,
    find_revit_pipes,
    start_revit,
    wait_for_revit_pipe,
)

if TYPE_CHECKING:
    from .dialog_resolver import StartupDialogResolver
    from .discovery import RevitInstance
    from .suite_leasing import SuiteLeaseStore

log = logging.getLogger(PLUGIN_NAME)

CONNECT_RETRIES = 3
CONNECT_RETRY_DELAY_S = 1.0


@dataclass
class ConnectionResult:
    bridge: RevitBridge | None = None
    dialog_resolver: StartupDialogResolver | None = None
    error: ConnectionError | None = None

    @property
    def ok(self) -> bool:
        return self.bridge is not None and self.bridge.connected


def ensure_bridge(
    *,
    current_bridge: RevitBridge | None,
    lease_store: SuiteLeaseStore | None,
    launch_timeout_s: float,
    version: int | None,
    explicit_pipe: str | None,
    suite_key: str,
    suite_path: str,
    prefer_fresh: bool = False,
) -> ConnectionResult:
    """Main entry point: return a connected bridge or an error."""
    if not prefer_fresh and current_bridge is not None and current_bridge.connected:
        return ConnectionResult(bridge=current_bridge)

    if explicit_pipe and not prefer_fresh:
        return _connect_explicit_pipe_or_exit(explicit_pipe)

    return _connect_discovered_or_launched(
        suite_key=suite_key,
        suite_path=suite_path,
        lease_store=lease_store,
        version=version,
        launch_timeout_s=launch_timeout_s,
        prefer_fresh=prefer_fresh,
    )


def _connect_discovered_or_launched(
    *,
    suite_key: str,
    suite_path: str,
    lease_store: SuiteLeaseStore | None,
    version: int | None,
    launch_timeout_s: float,
    prefer_fresh: bool,
) -> ConnectionResult:
    instances = instances_for_version(version)

    if lease_store is not None:
        bridge, _ = _try_reconnect_leased(lease_store, suite_key, suite_path, instances)
        if bridge is not None:
            return ConnectionResult(bridge=bridge)
        instances = instances_for_version(version)

    if not prefer_fresh:
        free = lease_store.find_free(suite_key, instances) if lease_store else instances
        bridge, error = _connect_and_lease(free, suite_key, suite_path, lease_store, "Assigned free instance")
        if bridge is not None:
            return ConnectionResult(bridge=bridge)
        if error is not None:
            return ConnectionResult(error=error)

    launch_version = _resolve_launch_version(version, instances)
    result = auto_launch(launch_version, launch_timeout_s)
    bridge, error = _connect_and_lease(
        [result.launched_instance], suite_key, suite_path, lease_store, "Spawned and leased",
    )
    return ConnectionResult(bridge=bridge, dialog_resolver=result.dialog_resolver, error=error)


@dataclass
class LaunchResult:
    launched_instance: RevitInstance
    dialog_resolver: StartupDialogResolver | None = None


def auto_launch(version: int, launch_timeout_s: float) -> LaunchResult:
    if find_revit_path(version) is None:
        pytest.exit(
            f"{PLUGIN_NAME}: Revit {version} is not installed on this machine.",
            returncode=EXIT_CODE_CONFIG_ERROR,
        )

    log.info("Launching Revit %d...", version)
    process_id = start_revit(version)

    dialog_resolver: StartupDialogResolver | None = None
    try:
        from .dialog_resolver import StartupDialogResolver

        resolver = StartupDialogResolver(process_id)
        resolver.start()
        dialog_resolver = resolver
    except ImportError:
        pass

    instance = wait_for_revit_pipe(version, timeout_s=launch_timeout_s)
    if instance is None:
        pytest.exit(
            f"{PLUGIN_NAME}: Revit {version} launched but Named Pipe did not appear within {launch_timeout_s}s.",
            returncode=EXIT_CODE_CONFIG_ERROR,
        )

    # Pipe availability can precede full UI/API readiness during cold start.
    time.sleep(2.0)

    log.info("Connected to Revit %d (pid=%d)", instance.version, instance.process_id)
    return LaunchResult(launched_instance=instance, dialog_resolver=dialog_resolver)


# ---------------------------------------------------------------------------
# Discovery helpers
# ---------------------------------------------------------------------------

def instances_for_version(version: int | None) -> list[RevitInstance]:
    instances = find_revit_pipes()
    if version is not None:
        return [i for i in instances if i.version == version]
    return sorted(instances, key=lambda i: i.version, reverse=True)


def find_instance_by_pid(
    instances: list[RevitInstance], process_id: int,
) -> RevitInstance | None:
    for instance in instances:
        if instance.process_id == process_id:
            return instance
    return None


def is_process_alive(process_id: int, expected_name: str = "Revit.exe") -> bool:
    """Check if process is alive AND matches the expected executable name.

    Plain PID-alive checks are vulnerable to Windows PID reuse.
    """
    PROCESS_QUERY_LIMITED_INFORMATION = 0x1000  # noqa: N806
    handle = ctypes.windll.kernel32.OpenProcess(  # type: ignore[attr-defined]
        PROCESS_QUERY_LIMITED_INFORMATION, False, int(process_id),
    )
    if handle == 0:
        return False
    try:
        buf = ctypes.create_unicode_buffer(260)
        size = ctypes.wintypes.DWORD(260)
        ok = ctypes.windll.kernel32.QueryFullProcessImageNameW(  # type: ignore[attr-defined]
            handle, 0, buf, ctypes.byref(size),
        )
        if not ok:
            return True  # can't verify name — assume alive conservatively
        return buf.value.lower().endswith(f"\\{expected_name.lower()}")
    finally:
        ctypes.windll.kernel32.CloseHandle(handle)  # type: ignore[attr-defined]


# ---------------------------------------------------------------------------
# Lease + connect internals
# ---------------------------------------------------------------------------

def _try_reconnect_leased(
    store: SuiteLeaseStore,
    suite_key: str,
    suite_path: str,
    instances: list[RevitInstance],
) -> tuple[RevitBridge | None, bool]:
    """Try reconnecting to a previously-leased Revit instance."""
    lease = store.get_suite_lease(suite_key)
    if lease is None:
        return None, False

    if not is_process_alive(lease.process_id):
        store.clear_suite(suite_key)
        return None, False

    leased_instance = find_instance_by_pid(instances, lease.process_id)
    if leased_instance is None:
        store.clear_suite(suite_key)
        return None, False

    bridge, _ = _connect_first_available([leased_instance])
    if bridge is not None:
        store.assign(suite_key, suite_path, leased_instance)
        log.info(
            "Reusing lease suite=%s pid=%d pipe=%s",
            suite_key, leased_instance.process_id, leased_instance.pipe_name,
        )
        return bridge, True

    store.clear_suite(suite_key)
    return None, False


def _connect_and_lease(
    instances: list[RevitInstance],
    suite_key: str,
    suite_path: str,
    store: SuiteLeaseStore | None,
    label: str,
) -> tuple[RevitBridge | None, ConnectionError | None]:
    bridge, selected, connect_error = _connect_first_available_with_instance(instances)
    if bridge is None or selected is None:
        return None, connect_error
    if store:
        store.assign(suite_key, suite_path, selected)
    log.info(
        "%s suite=%s pid=%d pipe=%s",
        label, suite_key, selected.process_id, selected.pipe_name,
    )
    return bridge, None


def _connect_first_available(
    instances: list[RevitInstance],
) -> tuple[RevitBridge | None, ConnectionError | None]:
    last_error: ConnectionError | None = None
    for instance in instances:
        try:
            return connect_pipe(instance.pipe_name), None
        except ConnectionError as exc:
            last_error = exc
    return None, last_error


def _connect_first_available_with_instance(
    instances: list[RevitInstance],
) -> tuple[RevitBridge | None, RevitInstance | None, ConnectionError | None]:
    last_error: ConnectionError | None = None
    for instance in instances:
        try:
            return connect_pipe(instance.pipe_name), instance, None
        except ConnectionError as exc:
            last_error = exc
    return None, None, last_error


def _connect_explicit_pipe_or_exit(pipe_name: str) -> ConnectionResult:
    try:
        return ConnectionResult(bridge=connect_pipe(pipe_name))
    except ConnectionError as exc:
        pytest.exit(
            f"{PLUGIN_NAME}: Could not connect to Revit: {exc}",
            returncode=EXIT_CODE_CONFIG_ERROR,
        )


def connect_pipe(pipe_name: str) -> RevitBridge:
    last_exc: ConnectionError | None = None
    for attempt in range(CONNECT_RETRIES):
        bridge = RevitBridge(pipe_name)
        try:
            bridge.connect()
            return bridge
        except ConnectionError as exc:
            bridge.disconnect()
            last_exc = exc
            if attempt < CONNECT_RETRIES - 1:
                log.debug("Pipe connect attempt %d failed, retrying...", attempt + 1)
                time.sleep(CONNECT_RETRY_DELAY_S)
    raise last_exc  # type: ignore[misc]


def _resolve_launch_version(
    version: int | None, instances: list[RevitInstance],
) -> int:
    if version is not None:
        return version
    if instances:
        return max(instances, key=lambda i: i.version).version
    pytest.exit(
        f"{PLUGIN_NAME}: --revit-version is required when no existing instances are available.",
        returncode=EXIT_CODE_CONFIG_ERROR,
    )
