"""pytest plugin — redirect test execution to a running host instance.

Thin hook orchestrator. Delegates to:
- ``connection`` — bridge lifecycle, discovery, lease, launch, retry
- ``reporting``  — remote result <-> pytest report mapping
- ``suite_lock`` — Windows Mutex + suite context resolution
"""

from __future__ import annotations

import os
from typing import TYPE_CHECKING

import pytest

from .connection import ensure_bridge
from .constants import (
    DEFAULT_HOST,
    DEFAULT_LAUNCH_TIMEOUT_S,
    DEFAULT_TEST_TIMEOUT_S,
    EXIT_CODE_CONFIG_ERROR,
    HOST_REGISTRY,
    OPT_FORCE_LAUNCH,
    OPT_HOST,
    OPT_LAUNCH_TIMEOUT,
    OPT_PER_TEST_TIMEOUT,
    OPT_PIPE,
    OPT_VERSION,
    PLUGIN_NAME,
)
from .ipy_collect import IpyTestFile, IpyTestItem, is_ipy_test_path
from .models import CaseResult
from .reporting import (
    emit_item_reports,
    fan_suite_results,
    host_pytest_args,
    run_remote_session,
    skip_all,
)
from .suite_leasing import SuiteLeaseStore
from .suite_lock import SuiteMutex, resolve_suite_context

if TYPE_CHECKING:
    from .bridge import HostBridge
    from .dialog_resolver import StartupDialogResolver

# ---------------------------------------------------------------------------
# Session state — only this file owns mutable globals
# ---------------------------------------------------------------------------

_bridge: HostBridge | None = None
_dialog_resolver: StartupDialogResolver | None = None
_lease_store: SuiteLeaseStore | None = None
_suite_mutex = SuiteMutex()

# Stash keys for cross-hook communication
_remote_results_key = pytest.StashKey[dict[str, list[CaseResult]]]()
_streamed_nodeids_key = pytest.StashKey[set[str]]()
_remote_collection_failed_key = pytest.StashKey[bool]()
_remote_collection_error_key = pytest.StashKey[str | None]()

_SUPPORTED_HOSTS = ", ".join(sorted(HOST_REGISTRY))


# ---------------------------------------------------------------------------
# Hooks — option registration
# ---------------------------------------------------------------------------


def pytest_addoption(parser: pytest.Parser) -> None:
    grp = parser.getgroup("host", "Host application testing (Revit, AutoCAD, Civil3D, etc.)")
    grp.addoption(
        "--host", dest=OPT_HOST, default=None,
        help=f"Host application name. Supported: {_SUPPORTED_HOSTS} (default: {DEFAULT_HOST}).",
    )
    grp.addoption(
        "--host-version", dest=OPT_VERSION, default=None,
        help="Host version (e.g. 2025, 8.0). Required when --force-launch is set.",
    )
    grp.addoption(
        "--per-test-timeout", dest=OPT_PER_TEST_TIMEOUT, default=None, type=float,
        help=(
            f"Per-test execution budget in seconds (default: {DEFAULT_TEST_TIMEOUT_S}). "
            "The tests/run pipe wait is this value times the number of collected tests."
        ),
    )
    grp.addoption(
        "--host-pipe", dest=OPT_PIPE, default=None,
        help="Explicit pipe name (bypasses auto-discovery).",
    )
    grp.addoption(
        "--force-launch", dest=OPT_FORCE_LAUNCH, action="store_true", default=False,
        help="Force-launch a new host instance (skip reusing existing). Requires --host-version.",
    )
    grp.addoption(
        "--launch-timeout", dest=OPT_LAUNCH_TIMEOUT, default=None, type=float,
        help=f"Seconds to wait for host pipe after launch (default: {DEFAULT_LAUNCH_TIMEOUT_S}).",
    )

    parser.addini(OPT_HOST, "Host application name", type="string", default=DEFAULT_HOST)
    parser.addini(OPT_VERSION, "Host version (e.g. 2025, 8.0)", type="string", default=None)
    parser.addini(
        OPT_PER_TEST_TIMEOUT,
        "Per-test execution budget (seconds); tests/run wait is this times collected tests",
        type="string",
        default=str(DEFAULT_TEST_TIMEOUT_S),
    )
    parser.addini(OPT_PIPE, "Explicit pipe name", type="string", default=None)
    parser.addini(OPT_FORCE_LAUNCH, "Force-launch a new host instance (skip reusing existing)", type="bool", default=False)
    parser.addini(OPT_LAUNCH_TIMEOUT, "Wait for host pipe after launch (seconds)", type="string", default=str(DEFAULT_LAUNCH_TIMEOUT_S))


# ---------------------------------------------------------------------------
# Hooks — session lifecycle
# ---------------------------------------------------------------------------


def pytest_configure(config: pytest.Config) -> None:
    config.addinivalue_line("markers", "revit: mark test to run inside host process")

    reportchars = getattr(config.option, "reportchars", "") or ""
    if "P" not in reportchars:
        config.option.reportchars = reportchars + "P"

    global _lease_store  # noqa
    _lease_store = SuiteLeaseStore()


@pytest.hookimpl(wrapper=True)
def pytest_collect_file(file_path, parent):
    collected = yield
    if not is_ipy_test_path(file_path):
        return collected
    return [IpyTestFile.from_parent(parent, path=file_path)]


@pytest.hookimpl(tryfirst=True)
def pytest_runtestloop(session: pytest.Session) -> bool | None:
    # firstresult=True: any non-None return suppresses pytest's default loop.
    if getattr(session.config.option, "collectonly", False):
        return True
    if os.environ.get("REVITDEVTOOL_PYTEST_DISABLE") == "1":
        return None

    host_name = _resolve_host_name(session.config)
    if not _ensure_bridge(session, host_name):
        skip_all(session, f"Not connected to {host_name}")
    elif session.items:
        _dispatch_remote_run(session)
    return True


@pytest.hookimpl(tryfirst=True)
def pytest_runtest_protocol(item: pytest.Item, nextitem: pytest.Item | None) -> bool:  # noqa
    results_by_nodeid = item.session.stash.get(_remote_results_key, None)
    if results_by_nodeid is None:
        return False

    streamed = item.session.stash.get(_streamed_nodeids_key, set())
    if item.nodeid in streamed:
        return True

    results = results_by_nodeid.get(item.nodeid, [])
    emit_item_reports(
        item, results,
        collection_failed=item.session.stash.get(_remote_collection_failed_key, False),
        collection_error_message=item.session.stash.get(_remote_collection_error_key, None),
    )
    return True


def pytest_unconfigure(config: pytest.Config) -> None:  # noqa
    global _bridge, _dialog_resolver, _lease_store
    _suite_mutex.release()
    if _dialog_resolver is not None:
        _dialog_resolver.stop()
        _dialog_resolver = None
    if _bridge is not None:
        _bridge.disconnect()
        _bridge = None
    _lease_store = None


# ---------------------------------------------------------------------------
# Internal — orchestration
# ---------------------------------------------------------------------------


def _dispatch_remote_run(session: pytest.Session) -> None:
    assert _bridge is not None
    per_test_timeout = _opt_float(session.config, OPT_PER_TEST_TIMEOUT, OPT_PER_TEST_TIMEOUT) or DEFAULT_TEST_TIMEOUT_S

    ipy_items = [item for item in session.items if isinstance(item, IpyTestItem)]
    py_items = [item for item in session.items if not isinstance(item, IpyTestItem)]

    results_by_nodeid: dict[str, list[CaseResult]] = {}
    streamed_nodeids: set[str] = set()
    collection_failed = False
    collection_error: str | None = None

    if py_items:
        prepare_timeout_s = (
            _opt_float(session.config, OPT_LAUNCH_TIMEOUT, OPT_LAUNCH_TIMEOUT)
            or DEFAULT_LAUNCH_TIMEOUT_S
        )
        py_results, py_streamed, py_failed, py_error = run_remote_session(
            session, _bridge, per_test_timeout, items=py_items,
            prepare_timeout_s=prepare_timeout_s,
            pytest_args=host_pytest_args(session.config),
        )
        results_by_nodeid.update(py_results)
        streamed_nodeids.update(py_streamed)
        collection_failed = collection_failed or py_failed
        collection_error = collection_error or py_error

    if ipy_items and not collection_failed and not session.shouldfail and not session.shouldstop:
        ipy_results, ipy_streamed, ipy_failed, ipy_error = run_remote_session(
            session, _bridge, per_test_timeout, items=ipy_items, ipy=True,
            pytest_args=host_pytest_args(session.config),
        )
        ipy_results = fan_suite_results(ipy_items, ipy_results)
        results_by_nodeid.update(ipy_results)
        streamed_nodeids.update(ipy_streamed)
        collection_failed = collection_failed or ipy_failed
        collection_error = collection_error or ipy_error

    session.stash[_remote_results_key] = results_by_nodeid
    session.stash[_streamed_nodeids_key] = streamed_nodeids
    session.stash[_remote_collection_failed_key] = collection_failed
    session.stash[_remote_collection_error_key] = collection_error

    for index, item in enumerate(session.items):
        if session.shouldstop or session.shouldfail:
            break
        nextitem = session.items[index + 1] if index + 1 < len(session.items) else None
        session.config.hook.pytest_runtest_protocol(item=item, nextitem=nextitem)


def _ensure_bridge(session: pytest.Session, host_name: str) -> bool:
    global _bridge, _dialog_resolver

    config = session.config
    suite_key, _ = resolve_suite_context(
        session, host_name, _opt(config, OPT_VERSION, OPT_VERSION),
    )

    explicit_pipe = _opt(config, OPT_PIPE, OPT_PIPE)
    if not explicit_pipe and not _suite_mutex.acquire(suite_key):
        pytest.exit(
            f"{PLUGIN_NAME}: Suite is already running in another pytest process (suite={suite_key}).",
            returncode=EXIT_CODE_CONFIG_ERROR,
        )

    force_launch = _opt_bool(config, OPT_FORCE_LAUNCH, OPT_FORCE_LAUNCH)
    if force_launch and _bridge is not None:
        _bridge.disconnect()
        _bridge = None

    result = ensure_bridge(
        current_bridge=_bridge,
        lease_store=_lease_store,
        launch_timeout_s=_opt_float(config, OPT_LAUNCH_TIMEOUT, OPT_LAUNCH_TIMEOUT) or DEFAULT_LAUNCH_TIMEOUT_S,
        host_name=host_name,
        version=_opt(config, OPT_VERSION, OPT_VERSION),
        explicit_pipe=explicit_pipe,
        suite_key=suite_key,
        force_launch=force_launch,
    )
    if result.dialog_resolver is not None:
        _dialog_resolver = result.dialog_resolver
    if result.error is not None and result.bridge is None:
        pytest.exit(
            f"{PLUGIN_NAME}: Could not connect to {host_name}: {result.error}",
            returncode=EXIT_CODE_CONFIG_ERROR,
        )
    _bridge = result.bridge
    return result.ok


# ---------------------------------------------------------------------------
# Internal — config helpers
# ---------------------------------------------------------------------------


def _resolve_host_name(config: pytest.Config) -> str:
    raw = _opt(config, OPT_HOST, OPT_HOST)
    return raw.strip().lower() if raw else DEFAULT_HOST


def _opt(config: pytest.Config, cli: str, ini: str) -> str | None:
    val = config.getoption(cli, default=None)
    if val is not None:
        return str(val)
    ini_val = config.getini(ini)
    return str(ini_val) if ini_val not in (None, "") else None


def _opt_float(config: pytest.Config, cli: str, ini: str) -> float | None:
    raw = _opt(config, cli, ini)
    return float(raw) if raw else None


def _opt_bool(config: pytest.Config, cli: str, ini: str) -> bool:
    cli_val = config.getoption(cli, default=None)
    if cli_val:
        return True
    ini_val = config.getini(ini)
    return bool(ini_val)
