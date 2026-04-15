"""pytest plugin — redirect test execution to a running Revit instance.

Thin hook orchestrator. Delegates to:
- ``connection`` — bridge lifecycle, discovery, lease, retry
- ``reporting``  — remote result ↔ pytest report mapping
- ``suite_lock`` — Windows Mutex + suite context resolution
"""

from __future__ import annotations

from typing import TYPE_CHECKING

import pytest

from .connection import ensure_bridge
from .constants import (
    DEFAULT_LAUNCH_TIMEOUT_S,
    DEFAULT_TEST_TIMEOUT_S,
    EXIT_CODE_CONFIG_ERROR,
    OPT_LAUNCH,
    OPT_LAUNCH_TIMEOUT,
    OPT_PIPE,
    OPT_TIMEOUT,
    OPT_VERSION,
    PHASE_CALL,
    PLUGIN_NAME,
)
from .models import CaseResult
from .reporting import emit_item_reports, run_remote_session, skip_all
from .suite_leasing import SuiteLeaseStore
from .suite_lock import SuiteMutex, resolve_suite_context

if TYPE_CHECKING:
    from .bridge import RevitBridge
    from .dialog_resolver import StartupDialogResolver

# ---------------------------------------------------------------------------
# Session state — only this file owns mutable globals
# ---------------------------------------------------------------------------

_bridge: RevitBridge | None = None
_dialog_resolver: StartupDialogResolver | None = None
_lease_store: SuiteLeaseStore | None = None
_suite_mutex = SuiteMutex()

_no_revit_key = pytest.StashKey[bool]()
_collect_only_key = pytest.StashKey[bool]()
_remote_results_key = pytest.StashKey[dict[str, list[CaseResult]]]()
_remote_collection_failed_key = pytest.StashKey[bool]()
_remote_collection_error_message_key = pytest.StashKey[str | None]()
_suite_key_stash = pytest.StashKey[str]()
_suite_path_stash = pytest.StashKey[str]()


# ---------------------------------------------------------------------------
# Hooks
# ---------------------------------------------------------------------------

def pytest_addoption(parser: pytest.Parser) -> None:
    grp = parser.getgroup("revit", "Revit API testing")
    grp.addoption(
        "--revit-version", dest=OPT_VERSION, default=None, type=int,
        help="Revit version year (e.g. 2025). Required when --revit-launch is set.",
    )
    grp.addoption(
        "--revit-timeout", dest=OPT_TIMEOUT, default=None, type=float,
        help=f"Per-test execution timeout in seconds (default: {DEFAULT_TEST_TIMEOUT_S}).",
    )
    grp.addoption(
        "--revit-pipe", dest=OPT_PIPE, default=None,
        help="Explicit pipe name (bypasses auto-discovery).",
    )
    grp.addoption(
        "--revit-launch", dest=OPT_LAUNCH, action="store_true", default=False,
        help="Auto-launch Revit if no running instance is found. Requires --revit-version.",
    )
    grp.addoption(
        "--revit-launch-timeout", dest=OPT_LAUNCH_TIMEOUT, default=None, type=float,
        help=f"Seconds to wait for Revit to start (default: {DEFAULT_LAUNCH_TIMEOUT_S}).",
    )

    parser.addini(OPT_VERSION, "Revit version year", type="string", default=None)
    parser.addini(OPT_TIMEOUT, "Per-test timeout (seconds)", type="string", default=str(DEFAULT_TEST_TIMEOUT_S))
    parser.addini(OPT_PIPE, "Explicit pipe name", type="string", default=None)
    parser.addini(OPT_LAUNCH, "Auto-launch Revit", type="bool", default=False)
    parser.addini(OPT_LAUNCH_TIMEOUT, "Launch timeout (seconds)", type="string", default=str(DEFAULT_LAUNCH_TIMEOUT_S))


def pytest_configure(config: pytest.Config) -> None:
    config.addinivalue_line("markers", "revit: mark test to run inside Revit process")

    global _lease_store  # noqa: PLW0603
    _lease_store = SuiteLeaseStore()

    config.stash[_collect_only_key] = _is_collect_only(config)
    config.stash[_remote_collection_failed_key] = False
    config.stash[_remote_collection_error_message_key] = None
    config.stash[_no_revit_key] = False


@pytest.hookimpl(tryfirst=True)
def pytest_runtestloop(session: pytest.Session) -> bool:
    if session.config.stash.get(_collect_only_key, False):
        return False

    if session.config.stash.get(_no_revit_key, True):
        skip_all(session, "No Revit instance available")
    elif not _ensure_bridge(session):
        skip_all(session, "Not connected to Revit")
    elif session.items:
        _dispatch_remote_run(session)
    return True


@pytest.hookimpl(tryfirst=True)
def pytest_runtest_protocol(item: pytest.Item, nextitem: pytest.Item | None) -> bool:  # noqa: ARG001
    results_by_nodeid = item.session.stash.get(_remote_results_key, None)
    if results_by_nodeid is None:
        return False

    reports = emit_item_reports(
        item,
        results_by_nodeid.get(item.nodeid, []),
        collection_failed=item.session.stash.get(_remote_collection_failed_key, False),
        collection_error_message=item.session.stash.get(_remote_collection_error_message_key, None),
    )
    for report in reports:
        if report.when == PHASE_CALL and report.failed:
            item.session.testsfailed += 1
    return True


def pytest_unconfigure(config: pytest.Config) -> None:  # noqa: ARG001
    global _bridge, _dialog_resolver, _lease_store  # noqa: PLW0603
    _suite_mutex.release()
    if _dialog_resolver is not None:
        _dialog_resolver.stop()
        _dialog_resolver = None
    if _bridge is not None:
        _bridge.disconnect()
        _bridge = None
    _lease_store = None


# ---------------------------------------------------------------------------
# Internal orchestration
# ---------------------------------------------------------------------------

def _dispatch_remote_run(session: pytest.Session) -> None:
    assert _bridge is not None
    per_test_timeout = _opt_float(session.config, OPT_TIMEOUT, OPT_TIMEOUT) or DEFAULT_TEST_TIMEOUT_S
    results_by_nodeid, collection_failed, collection_error_message = run_remote_session(
        session, _bridge, per_test_timeout,
    )
    session.stash[_remote_results_key] = results_by_nodeid
    session.stash[_remote_collection_failed_key] = collection_failed
    session.stash[_remote_collection_error_message_key] = collection_error_message
    for index, item in enumerate(session.items):
        nextitem = session.items[index + 1] if index + 1 < len(session.items) else None
        session.config.hook.pytest_runtest_protocol(item=item, nextitem=nextitem)


def _ensure_bridge(session: pytest.Session) -> bool:
    global _bridge, _dialog_resolver  # noqa: PLW0603

    config = session.config
    suite_key, suite_path = resolve_suite_context(session)
    config.stash[_suite_key_stash] = suite_key
    config.stash[_suite_path_stash] = suite_path

    explicit_pipe = _opt(config, OPT_PIPE, OPT_PIPE)
    if not explicit_pipe and not _suite_mutex.acquire(suite_key):
        pytest.exit(
            f"{PLUGIN_NAME}: Suite is already running in another pytest process (suite={suite_key}).",
            returncode=EXIT_CODE_CONFIG_ERROR,
        )

    result = ensure_bridge(
        current_bridge=_bridge,
        lease_store=_lease_store,
        launch_timeout_s=_opt_float(config, OPT_LAUNCH_TIMEOUT, OPT_LAUNCH_TIMEOUT) or DEFAULT_LAUNCH_TIMEOUT_S,
        version=_opt_int(config, OPT_VERSION, OPT_VERSION),
        explicit_pipe=explicit_pipe,
        suite_key=suite_key,
        suite_path=suite_path,
    )
    if result.dialog_resolver is not None:
        _dialog_resolver = result.dialog_resolver
    if result.error is not None and result.bridge is None:
        pytest.exit(
            f"{PLUGIN_NAME}: Could not connect to Revit: {result.error}",
            returncode=EXIT_CODE_CONFIG_ERROR,
        )
    _bridge = result.bridge
    return result.ok


# ---------------------------------------------------------------------------
# Config helpers
# ---------------------------------------------------------------------------

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


def _is_collect_only(config: pytest.Config) -> bool:
    option = getattr(config, "option", None)
    return bool(getattr(option, "collectonly", False))
