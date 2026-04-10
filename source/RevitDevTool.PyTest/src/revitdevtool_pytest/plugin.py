"""pytest plugin — redirect test execution to a running Revit instance.

``--revit-launch`` requires ``--revit-version`` — no fallback guessing.
"""

from __future__ import annotations

import sys
from typing import TYPE_CHECKING

import pytest

from .bridge import RevitBridge
from .constants import (
    DEFAULT_LAUNCH_TIMEOUT_S,
    DEFAULT_TEST_TIMEOUT_S,
    EXIT_CODE_CONFIG_ERROR,
    OPT_LAUNCH,
    OPT_LAUNCH_TIMEOUT,
    OPT_PIPE,
    OPT_TIMEOUT,
    OPT_VERSION,
    PLUGIN_NAME,
)
from .discovery import find_revit_path, find_revit_pipes, launch_revit, select_instance
from .models import TestOutcome
from .serializer import serialize_test

if TYPE_CHECKING:
    from .dialog_resolver import StartupDialogResolver
    from .discovery import RevitInstance

_bridge: RevitBridge | None = None
_dialog_resolver: StartupDialogResolver | None = None
_no_revit_key = pytest.StashKey[bool]()


def pytest_addoption(parser: pytest.Parser) -> None:
    grp = parser.getgroup("revit", "Revit API testing")
    grp.addoption("--revit-version", dest=OPT_VERSION, default=None, type=int,
                   help="Revit version year (e.g. 2025). Required when --revit-launch is set.")
    grp.addoption("--revit-timeout", dest=OPT_TIMEOUT, default=None, type=float,
                   help=f"Per-test execution timeout in seconds (default: {DEFAULT_TEST_TIMEOUT_S}).")
    grp.addoption("--revit-pipe", dest=OPT_PIPE, default=None,
                   help="Explicit pipe name (bypasses auto-discovery).")
    grp.addoption("--revit-launch", dest=OPT_LAUNCH, action="store_true", default=False,
                   help="Auto-launch Revit if no running instance is found. Requires --revit-version.")
    grp.addoption("--revit-launch-timeout", dest=OPT_LAUNCH_TIMEOUT, default=None, type=float,
                   help=f"Seconds to wait for Revit to start (default: {DEFAULT_LAUNCH_TIMEOUT_S}).")

    parser.addini(OPT_VERSION, "Revit version year", type="string", default=None)
    parser.addini(OPT_TIMEOUT, "Per-test timeout (seconds)", type="string",
                  default=str(DEFAULT_TEST_TIMEOUT_S))
    parser.addini(OPT_PIPE, "Explicit pipe name", type="string", default=None)
    parser.addini(OPT_LAUNCH, "Auto-launch Revit", type="bool", default=False)
    parser.addini(OPT_LAUNCH_TIMEOUT, "Launch timeout (seconds)", type="string",
                  default=str(DEFAULT_LAUNCH_TIMEOUT_S))


def pytest_configure(config: pytest.Config) -> None:
    config.addinivalue_line("markers", "revit: mark test to run inside Revit process")

    global _bridge, _dialog_resolver

    explicit_pipe = _opt(config, OPT_PIPE, OPT_PIPE)
    if explicit_pipe:
        pipe_name = explicit_pipe
    else:
        version = _opt_int(config, OPT_VERSION, OPT_VERSION)
        want_launch = _opt_bool(config, OPT_LAUNCH, OPT_LAUNCH)

        if want_launch and version is None:
            pytest.exit(
                f"{PLUGIN_NAME}: --revit-launch requires --revit-version (e.g. --revit-version=2025)",
                returncode=EXIT_CODE_CONFIG_ERROR,
            )

        instance = select_instance(find_revit_pipes(), version)

        if instance is None and want_launch:
            instance = _auto_launch(version, config)  # type: ignore[arg-type]

        if instance is None:
            config.stash[_no_revit_key] = True
            return
        pipe_name = instance.pipe_name

    bridge = RevitBridge(pipe_name)
    try:
        bridge.connect()
        _bridge = bridge
        config.stash[_no_revit_key] = False
    except ConnectionError as exc:
        pytest.exit(f"{PLUGIN_NAME}: Could not connect to Revit: {exc}", returncode=EXIT_CODE_CONFIG_ERROR)


@pytest.hookimpl(tryfirst=True)
def pytest_pyfunc_call(pyfuncitem: pytest.Function) -> object:
    """Replace local execution with remote execution inside Revit.

    Returning ``True`` tells pytest to skip calling the test function locally.
    """
    if pyfuncitem.config.stash.get(_no_revit_key, True):
        pytest.skip("No Revit instance available")

    if _bridge is None or not _bridge.connected:
        pytest.skip("Not connected to Revit")

    timeout = _opt_float(pyfuncitem.config, OPT_TIMEOUT, OPT_TIMEOUT) or DEFAULT_TEST_TIMEOUT_S
    result = _bridge.execute_test(serialize_test(pyfuncitem), timeout_s=timeout)

    if result.stdout:
        sys.stdout.write(result.stdout)

    if result.outcome == TestOutcome.PASSED:
        return True

    if result.outcome == TestOutcome.SKIPPED:
        pytest.skip(result.message)

    msg = result.message
    if result.outcome != TestOutcome.FAILED:
        msg = f"Test execution error: {msg}"
    if result.traceback:
        msg += "\n" + result.traceback
    pytest.fail(msg, pytrace=False)


def pytest_unconfigure(config: pytest.Config) -> None:
    global _bridge, _dialog_resolver
    if _dialog_resolver is not None:
        _dialog_resolver.stop()
        _dialog_resolver = None
    if _bridge is not None:
        _bridge.disconnect()
        _bridge = None


def _auto_launch(version: int, config: pytest.Config) -> RevitInstance:
    """Launch exactly the requested Revit version, or fail fast.

    Every failure path calls ``pytest.exit()`` — this function never returns ``None``.
    """
    global _dialog_resolver

    timeout = _opt_float(config, OPT_LAUNCH_TIMEOUT, OPT_LAUNCH_TIMEOUT) or DEFAULT_LAUNCH_TIMEOUT_S

    if find_revit_path(version) is None:
        pytest.exit(
            f"{PLUGIN_NAME}: Revit {version} is not installed on this machine.",
            returncode=EXIT_CODE_CONFIG_ERROR,
        )

    print(f"{PLUGIN_NAME}: Launching Revit {version}...")
    instance = launch_revit(version, wait_timeout_s=timeout)

    if instance is None:
        pytest.exit(
            f"{PLUGIN_NAME}: Revit {version} launched but Named Pipe did not appear within {timeout}s.",
            returncode=EXIT_CODE_CONFIG_ERROR,
        )

    try:
        from .dialog_resolver import StartupDialogResolver as _Resolver

        _dialog_resolver = _Resolver(instance.process_id)
        _dialog_resolver.start()
    except ImportError:
        pass

    print(f"{PLUGIN_NAME}: Connected to Revit {instance.version} (pid={instance.process_id})")
    return instance


def _opt(config: pytest.Config, cli: str, ini: str) -> str | None:
    val = config.getoption(cli, default=None)
    if val is not None:
        return str(val)
    ini_val = config.getini(ini)
    return str(ini_val) if ini_val not in (None, "") else None


def _opt_int(config: pytest.Config, cli: str, ini: str) -> int | None:
    raw = _opt(config, cli, ini)
    return int(raw) if raw else None


def _opt_float(config: pytest.Config, cli: str, ini: str) -> float | None:
    raw = _opt(config, cli, ini)
    return float(raw) if raw else None


def _opt_bool(config: pytest.Config, cli: str, ini: str) -> bool:
    if config.getoption(cli, default=False):
        return True
    ini_val = config.getini(ini)
    if isinstance(ini_val, bool):
        return ini_val
    return str(ini_val).lower() in ("true", "1", "yes") if ini_val else False
