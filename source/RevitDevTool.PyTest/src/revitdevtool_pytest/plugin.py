"""pytest plugin — redirect test execution to a running Revit instance.

Uses ``tests/run`` protocol: pytest collects locally, nodeids are sent to
Revit where ``PytestRunner.py`` executes a real ``pytest.main()`` session.
Results are mapped back to local pytest reports for IDE display.

``--revit-launch`` requires ``--revit-version`` — no fallback guessing.
"""

from __future__ import annotations
from typing import TYPE_CHECKING, Literal

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
    OUTCOME_ERROR,
    OUTCOME_FAILED,
    OUTCOME_PASSED,
    OUTCOME_SKIPPED,
    OUTCOME_XFAILED,
    OUTCOME_XPASSED,
    PHASE_CALL,
    PLUGIN_NAME,
    RUN_MODE_SESSION,
)
from .discovery import find_revit_path, find_revit_pipes, start_revit, wait_for_revit_pipe
from .models import CaseResult

_PytestOutcome = Literal["passed", "failed", "skipped"]

if TYPE_CHECKING:
    from .dialog_resolver import StartupDialogResolver
    from .discovery import RevitInstance

_bridge: RevitBridge | None = None
_dialog_resolver: StartupDialogResolver | None = None
_no_revit_key = pytest.StashKey[bool]()
_collect_only_key = pytest.StashKey[bool]()
_remote_results_key = pytest.StashKey[dict[str, list[CaseResult]]]()

_VALID_REPORT_OUTCOMES = frozenset({OUTCOME_PASSED, OUTCOME_FAILED, OUTCOME_SKIPPED})


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

    global _bridge, _dialog_resolver  # noqa: PLW0603
    collect_only = _is_collect_only(config)
    config.stash[_collect_only_key] = collect_only

    if collect_only:
        config.stash[_no_revit_key] = False
        return

    explicit_pipe = _opt(config, OPT_PIPE, OPT_PIPE)
    if explicit_pipe:
        _bridge = _connect_explicit_pipe_or_exit(explicit_pipe)
        config.stash[_no_revit_key] = False
        return

    bridge, connect_error = _connect_discovered_or_launched(config)
    if bridge is None and connect_error is None:
        config.stash[_no_revit_key] = True
        return
    if bridge is None and connect_error is not None:
        pytest.exit(f"{PLUGIN_NAME}: Could not connect to Revit: {connect_error}", returncode=EXIT_CODE_CONFIG_ERROR)

    _bridge = bridge
    config.stash[_no_revit_key] = False


@pytest.hookimpl(tryfirst=True)
def pytest_runtestloop(session: pytest.Session) -> bool:
    if session.config.stash.get(_collect_only_key, False):
        return False

    if session.config.stash.get(_no_revit_key, True):
        _skip_all(session, "No Revit instance available")
    elif _bridge is None or not _bridge.connected:
        _skip_all(session, "Not connected to Revit")
    elif session.items:
        session.stash[_remote_results_key] = _run_remote_session(session)
        for index, item in enumerate(session.items):
            nextitem = session.items[index + 1] if index + 1 < len(session.items) else None
            session.config.hook.pytest_runtest_protocol(item=item, nextitem=nextitem)
    return True


@pytest.hookimpl(tryfirst=True)
def pytest_runtest_protocol(item: pytest.Item, nextitem: pytest.Item | None) -> bool:  # noqa: ARG001
    results_by_nodeid = item.session.stash.get(_remote_results_key, None)
    if results_by_nodeid is None:
        return False

    reports = _emit_item_reports(item, results_by_nodeid.get(item.nodeid, []))
    for report in reports:
        if report.when == PHASE_CALL and report.failed:
            item.session.testsfailed += 1
    return True


def _run_remote_session(session: pytest.Session) -> dict[str, list[CaseResult]]:
    workspace_root = str(session.config.rootdir)
    per_test_timeout = _opt_float(session.config, OPT_TIMEOUT, OPT_TIMEOUT) or DEFAULT_TEST_TIMEOUT_S
    total_timeout = per_test_timeout * max(len(session.items), 1)

    nodeids = [item.nodeid for item in session.items]

    assert _bridge is not None  # guarded by caller

    try:
        response = _bridge.run_tests(
            workspace_root=workspace_root,
            test_root=workspace_root,
            nodeids=nodeids,
            mode=RUN_MODE_SESSION,
            timeout_s=total_timeout,
        )
    except OSError as exc:
        _fail_all(session, f"{PLUGIN_NAME}: Remote execution failed: {exc}")
        return {}

    for err in response.collection_errors:
        _report_collection_error(session, err.nodeid, err.message, err.traceback)

    results_by_nodeid: dict[str, list[CaseResult]] = {}
    for r in response.results:
        results_by_nodeid.setdefault(r.nodeid, []).append(r)

    return results_by_nodeid


def pytest_unconfigure(config: pytest.Config) -> None:  # noqa
    global _bridge, _dialog_resolver  # noqa: PLW0603
    if _dialog_resolver is not None:
        _dialog_resolver.stop()
        _dialog_resolver = None
    if _bridge is not None:
        _bridge.disconnect()
        _bridge = None


def _auto_launch(version: int, config: pytest.Config) -> RevitInstance:
    global _dialog_resolver  # noqa: PLW0603

    timeout = _opt_float(config, OPT_LAUNCH_TIMEOUT, OPT_LAUNCH_TIMEOUT) or DEFAULT_LAUNCH_TIMEOUT_S

    if find_revit_path(version) is None:
        pytest.exit(
            f"{PLUGIN_NAME}: Revit {version} is not installed on this machine.",
            returncode=EXIT_CODE_CONFIG_ERROR,
        )

    print(f"{PLUGIN_NAME}: Launching Revit {version}...")

    process_id = start_revit(version)

    try:
        from .dialog_resolver import StartupDialogResolver

        resolver = StartupDialogResolver(process_id)
        resolver.start()
        _dialog_resolver = resolver
    except ImportError:
        pass

    instance = wait_for_revit_pipe(version, timeout_s=timeout)

    if instance is None:
        pytest.exit(
            f"{PLUGIN_NAME}: Revit {version} launched but Named Pipe did not appear within {timeout}s.",
            returncode=EXIT_CODE_CONFIG_ERROR,
        )

    print(f"{PLUGIN_NAME}: Connected to Revit {instance.version} (pid={instance.process_id})")
    return instance


def _instances_for_version(version: int | None) -> list[RevitInstance]:
    instances = find_revit_pipes()
    if version is not None:
        return [instance for instance in instances if instance.version == version]
    return sorted(instances, key=lambda instance: instance.version, reverse=True)


def _connect_explicit_pipe_or_exit(pipe_name: str) -> RevitBridge:
    try:
        return _connect_pipe(pipe_name)
    except ConnectionError as exc:
        pytest.exit(f"{PLUGIN_NAME}: Could not connect to Revit: {exc}", returncode=EXIT_CODE_CONFIG_ERROR)


def _connect_discovered_or_launched(config: pytest.Config) -> tuple[RevitBridge | None, ConnectionError | None]:
    version = _opt_int(config, OPT_VERSION, OPT_VERSION)
    want_launch = _opt_bool(config, OPT_LAUNCH, OPT_LAUNCH)
    _validate_launch_options(version, want_launch)

    bridge, connect_error = _connect_first_available(_instances_for_version(version))
    if bridge is not None or not want_launch:
        return bridge, connect_error

    launched = _auto_launch(version, config)  # type: ignore[arg-type]
    return _connect_first_available([launched])


def _validate_launch_options(version: int | None, want_launch: bool) -> None:
    if want_launch and version is None:
        pytest.exit(
            f"{PLUGIN_NAME}: --revit-launch requires --revit-version (e.g. --revit-version=2025)",
            returncode=EXIT_CODE_CONFIG_ERROR,
        )


def _connect_first_available(
    instances: list[RevitInstance],
) -> tuple[RevitBridge | None, ConnectionError | None]:
    last_error: ConnectionError | None = None
    for instance in instances:
        try:
            bridge = _connect_pipe(instance.pipe_name)
            return bridge, None
        except ConnectionError as exc:
            last_error = exc
    return None, last_error


def _connect_pipe(pipe_name: str) -> RevitBridge:
    bridge = RevitBridge(pipe_name)
    try:
        bridge.connect()
        return bridge
    except ConnectionError:
        bridge.disconnect()
        raise


def _emit_item_reports(item: pytest.Item, results: list[CaseResult]) -> list[pytest.TestReport]:
    ihook = item.ihook
    ihook.pytest_runtest_logstart(nodeid=item.nodeid, location=item.location)

    emitted: list[pytest.TestReport] = []
    if not results:
        report = _make_error_report(item, "No result received from Revit for this test.")
        ihook.pytest_runtest_logreport(report=report)
        emitted.append(report)
    else:
        for r in results:
            report = _make_report(item, r)
            ihook.pytest_runtest_logreport(report=report)
            emitted.append(report)

    ihook.pytest_runtest_logfinish(nodeid=item.nodeid, location=item.location)
    return emitted


def _make_report(item: pytest.Item, result: CaseResult) -> pytest.TestReport:
    outcome, wasxfail = _normalize_outcome(result)

    longrepr: str | tuple[str, int, str] | None = None
    if outcome == OUTCOME_FAILED and (result.message or result.traceback):
        longrepr = result.traceback if result.traceback else result.message
    elif outcome == OUTCOME_SKIPPED:
        longrepr = ("", -1, f"Skipped: {result.message}" if result.message else "Skipped")

    sections: list[tuple[str, str]] = []
    if result.stdout:
        sections.append(("Captured stdout", result.stdout))
    if result.stderr:
        sections.append(("Captured stderr", result.stderr))

    report = pytest.TestReport(
        nodeid=item.nodeid,
        location=item.location,
        keywords=dict(item.keywords),
        outcome=outcome,
        longrepr=longrepr, # type: ignore[arg-type]  # str accepted at runtime
        when=result.phase, # type: ignore[arg-type]  # str accepted at runtime
        duration=result.duration_ms / 1000.0,
        sections=sections,
    )
    if wasxfail:
        report.wasxfail = wasxfail  # type: ignore[attr-defined]
    return report


def _normalize_outcome(result: CaseResult) -> tuple[_PytestOutcome, str]:
    """Map remote outcome to pytest-native outcome + optional wasxfail reason."""
    outcome = result.outcome
    if outcome == OUTCOME_XFAILED:
        return OUTCOME_SKIPPED, result.message or "expected failure" # type: ignore[arg-type]  # str accepted at runtime
    if outcome == OUTCOME_XPASSED:
        return OUTCOME_FAILED, result.message or "expected failure but passed" # type: ignore[arg-type]  # str accepted at runtime
    if outcome == OUTCOME_ERROR or outcome not in _VALID_REPORT_OUTCOMES:
        return OUTCOME_FAILED, "" # type: ignore[arg-type]  # str accepted at runtime
    return outcome, ""  # type: ignore[return-value]  # validated by _VALID_REPORT_OUTCOMES check


def _make_error_report(item: pytest.Item, message: str) -> pytest.TestReport:
    return pytest.TestReport(
        nodeid=item.nodeid,
        location=item.location,
        keywords=dict(item.keywords),
        outcome=OUTCOME_FAILED, # type: ignore[arg-type]  # str accepted at runtime
        longrepr=message, # type: ignore[arg-type]  # str accepted at runtime
        when=PHASE_CALL, # type: ignore[arg-type]  # str accepted at runtime
    )


def _skip_all(session: pytest.Session, reason: str) -> None:
    for item in session.items:
        ihook = item.ihook
        ihook.pytest_runtest_logstart(nodeid=item.nodeid, location=item.location)
        report = pytest.TestReport(
            nodeid=item.nodeid,
            location=item.location,
            keywords=dict(item.keywords),
            outcome=OUTCOME_SKIPPED, # type: ignore[arg-type]  # str accepted at runtime
            longrepr=("", -1, f"Skipped: {reason}"), # type: ignore[arg-type]  # str accepted at runtime
            when=PHASE_CALL, # type: ignore[arg-type]  # str accepted at runtime
        )
        ihook.pytest_runtest_logreport(report=report)
        ihook.pytest_runtest_logfinish(nodeid=item.nodeid, location=item.location)


def _fail_all(session: pytest.Session, message: str) -> None:
    for item in session.items:
        ihook = item.ihook
        ihook.pytest_runtest_logstart(nodeid=item.nodeid, location=item.location)
        ihook.pytest_runtest_logreport(report=_make_error_report(item, message))
        ihook.pytest_runtest_logfinish(nodeid=item.nodeid, location=item.location)


def _report_collection_error(session: pytest.Session, nodeid: str, message: str, tb: str) -> None:
    full = f"{message}\n{tb}" if tb else message
    session.config.hook.pytest_collectreport(
        report=pytest.CollectReport(
            nodeid=nodeid or "<collection>",
            outcome=OUTCOME_FAILED, # type: ignore[arg-type]  # str accepted at runtime
            longrepr=full,  # type: ignore[arg-type]  # str accepted at runtime
            result=[],
        ),
    )


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


def _is_collect_only(config: pytest.Config) -> bool:
    option = getattr(config, "option", None)
    return bool(getattr(option, "collectonly", False))
