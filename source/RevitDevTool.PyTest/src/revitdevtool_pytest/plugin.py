"""pytest plugin — redirect test execution to a running Revit instance.

Uses ``tests/run`` protocol: pytest collects locally, nodeids are sent to
Revit where ``PytestRunner.py`` executes a real ``pytest.main()`` session.
Results are mapped back to local pytest reports for IDE display.

``--revit-launch`` requires ``--revit-version`` — no fallback guessing.
"""

from __future__ import annotations
import hashlib
import ctypes
import os
import time
from pathlib import Path
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
from .models import CaseResult, RunResponse
from .suite_leasing import SuiteLeaseStore

_PytestOutcome = Literal["passed", "failed", "skipped"]

if TYPE_CHECKING:
    from .dialog_resolver import StartupDialogResolver
    from .discovery import RevitInstance

_bridge: RevitBridge | None = None
_dialog_resolver: StartupDialogResolver | None = None
_lease_store = SuiteLeaseStore()
_no_revit_key = pytest.StashKey[bool]()
_collect_only_key = pytest.StashKey[bool]()
_remote_results_key = pytest.StashKey[dict[str, list[CaseResult]]]()
_remote_collection_failed_key = pytest.StashKey[bool]()
_remote_collection_error_message_key = pytest.StashKey[str | None]()
_suite_key_stash = pytest.StashKey[str]()
_suite_path_stash = pytest.StashKey[str]()
_suite_lock_path_stash = pytest.StashKey[Path | None]()

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
    config.stash[_remote_collection_failed_key] = False
    config.stash[_remote_collection_error_message_key] = None
    config.stash[_suite_lock_path_stash] = None

    if collect_only:
        config.stash[_no_revit_key] = False
        return

    config.stash[_no_revit_key] = False


@pytest.hookimpl(tryfirst=True)
def pytest_runtestloop(session: pytest.Session) -> bool:
    if session.config.stash.get(_collect_only_key, False):
        return False

    if session.config.stash.get(_no_revit_key, True):
        _skip_all(session, "No Revit instance available")
    elif not _ensure_bridge(session):
        _skip_all(session, "Not connected to Revit")
    elif session.items:
        results_by_nodeid, collection_failed, collection_error_message = _run_remote_session(session)
        session.stash[_remote_results_key] = results_by_nodeid
        session.stash[_remote_collection_failed_key] = collection_failed
        session.stash[_remote_collection_error_message_key] = collection_error_message
        for index, item in enumerate(session.items):
            nextitem = session.items[index + 1] if index + 1 < len(session.items) else None
            session.config.hook.pytest_runtest_protocol(item=item, nextitem=nextitem)
    return True


@pytest.hookimpl(tryfirst=True)
def pytest_runtest_protocol(item: pytest.Item, nextitem: pytest.Item | None) -> bool:  # noqa: ARG001
    results_by_nodeid = item.session.stash.get(_remote_results_key, None)
    if results_by_nodeid is None:
        return False
    collection_failed = item.session.stash.get(_remote_collection_failed_key, False)
    collection_error_message = item.session.stash.get(_remote_collection_error_message_key, None)

    reports = _emit_item_reports(
        item,
        results_by_nodeid.get(item.nodeid, []),
        collection_failed=collection_failed,
        collection_error_message=collection_error_message,
    )
    for report in reports:
        if report.when == PHASE_CALL and report.failed:
            item.session.testsfailed += 1
    return True


def _run_remote_session(session: pytest.Session) -> tuple[dict[str, list[CaseResult]], bool, str | None]:
    workspace_root = str(session.config.rootdir)
    per_test_timeout = _opt_float(session.config, OPT_TIMEOUT, OPT_TIMEOUT) or DEFAULT_TEST_TIMEOUT_S
    total_timeout = per_test_timeout * max(len(session.items), 1)

    nodeids = [item.nodeid for item in session.items]

    response = _request_remote_run(
        workspace_root=workspace_root,
        nodeids=nodeids,
        timeout_s=total_timeout,
    )
    if response is None:
        _fail_all(session, f"{PLUGIN_NAME}: Remote execution failed.")
        return {}, False, None

    collection_error_message: str | None = None
    for err in response.collection_errors:
        _report_collection_error(session, err.nodeid, err.message, err.traceback)
        if collection_error_message is None:
            collection_error_message = err.message or err.traceback or "Remote collection failed."

    collection_failed = _is_global_collection_failure(response)
    if collection_failed:
        return {}, True, collection_error_message

    results_by_nodeid: dict[str, list[CaseResult]] = {}
    for r in response.results:
        results_by_nodeid.setdefault(r.nodeid, []).append(r)

    return results_by_nodeid, False, None


def _request_remote_run(
    workspace_root: str,
    nodeids: list[str],
    timeout_s: float,
) -> RunResponse | None:
    assert _bridge is not None  # guarded by caller
    try:
        return _bridge.run_tests(
            workspace_root=workspace_root,
            test_root=workspace_root,
            nodeids=nodeids,
            mode=RUN_MODE_SESSION,
            timeout_s=timeout_s,
        )
    except Exception as exc:  # noqa: BLE001
        print(f"{PLUGIN_NAME}: Remote request failed: {exc}")
        return None


def _is_global_collection_failure(response: RunResponse) -> bool:
    return bool(response.collection_errors) and not response.results


def pytest_unconfigure(config: pytest.Config) -> None:  # noqa
    global _bridge, _dialog_resolver  # noqa: PLW0603
    _release_suite_lock(config)
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

    # Pipe availability can precede full UI/API readiness during cold start.
    # Give Revit a short settling window to reduce first-request ValueFactory failures.
    time.sleep(2.0)

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


def _ensure_bridge(session: pytest.Session) -> bool:
    global _bridge  # noqa: PLW0603
    return _ensure_bridge_core(session, prefer_fresh=False)


def _ensure_bridge_core(session: pytest.Session, prefer_fresh: bool) -> bool:
    global _bridge  # noqa: PLW0603

    if not prefer_fresh and _bridge is not None and _bridge.connected:
        return True

    config = session.config
    explicit_pipe = _opt(config, OPT_PIPE, OPT_PIPE)
    if explicit_pipe and not prefer_fresh:
        _bridge = _connect_explicit_pipe_or_exit(explicit_pipe)
        return True

    suite_key, suite_path = _resolve_suite_context(session)
    config.stash[_suite_key_stash] = suite_key
    config.stash[_suite_path_stash] = suite_path
    if not _ensure_suite_lock(config, suite_key):
        pytest.exit(
            f"{PLUGIN_NAME}: Suite is already running in another pytest process (suite={suite_key}).",
            returncode=EXIT_CODE_CONFIG_ERROR,
        )

    bridge, connect_error = _connect_discovered_or_launched(
        config,
        suite_key,
        suite_path,
        prefer_fresh=prefer_fresh,
    )
    if bridge is None and connect_error is not None:
        pytest.exit(
            f"{PLUGIN_NAME}: Could not connect to Revit: {connect_error}",
            returncode=EXIT_CODE_CONFIG_ERROR,
        )

    _bridge = bridge
    return _bridge is not None and _bridge.connected


def _resolve_suite_context(session: pytest.Session) -> tuple[str, str]:
    root_path = Path(str(session.config.rootpath)).resolve()
    conftest_paths = {
        _nearest_conftest(Path(str(item.path)).resolve(), root_path)
        for item in session.items
    }
    if len(conftest_paths) > 1:
        formatted = ", ".join(sorted(str(path) for path in conftest_paths))
        pytest.exit(
            f"{PLUGIN_NAME}: A run can target only one conftest.py suite. Found: {formatted}",
            returncode=EXIT_CODE_CONFIG_ERROR,
        )

    suite_path = conftest_paths.pop() if conftest_paths else root_path
    suite_path_text = str(suite_path)
    return _suite_key_for_path(suite_path_text), suite_path_text


def _suite_lock_dir() -> Path:
    return Path.home() / ".revitdevtool_pytest" / "suite-locks"


def _ensure_suite_lock(config: pytest.Config, suite_key: str) -> bool:
    existing = config.stash.get(_suite_lock_path_stash, None)
    if existing is not None:
        return True

    lock_dir = _suite_lock_dir()
    lock_dir.mkdir(parents=True, exist_ok=True)
    lock_path = lock_dir / f"{suite_key}.lock"
    payload = f"{os.getpid()}\n{time.time()}\n"

    try:
        fd = os.open(str(lock_path), os.O_CREAT | os.O_EXCL | os.O_WRONLY)
        with os.fdopen(fd, "w", encoding="utf-8") as handle:
            handle.write(payload)
        config.stash[_suite_lock_path_stash] = lock_path
        return True
    except FileExistsError:
        owner_pid = _read_lock_owner_pid(lock_path)
        if owner_pid is not None and not _is_process_alive(owner_pid):
            try:
                lock_path.unlink(missing_ok=True)
            except Exception:
                return False
            return _ensure_suite_lock(config, suite_key)
        return False


def _read_lock_owner_pid(lock_path: Path) -> int | None:
    try:
        first_line = lock_path.read_text(encoding="utf-8").splitlines()[0]
        return int(first_line)
    except Exception:
        return None


def _release_suite_lock(config: pytest.Config) -> None:
    lock_path = config.stash.get(_suite_lock_path_stash, None)
    if lock_path is None:
        return
    try:
        lock_path.unlink(missing_ok=True)
    except Exception:
        pass
    config.stash[_suite_lock_path_stash] = None


def _nearest_conftest(file_path: Path, root_path: Path) -> Path:
    current = file_path.parent
    normalized_root = root_path.resolve()
    while True:
        candidate = current / "conftest.py"
        if candidate.is_file():
            return candidate.resolve()
        if current == normalized_root or current.parent == current:
            return normalized_root
        current = current.parent


def _suite_key_for_path(path: str) -> str:
    normalized = str(Path(path).resolve()).lower()
    digest = hashlib.sha1(normalized.encode("utf-8")).hexdigest()
    return digest[:16]


def _connect_discovered_or_launched(
    config: pytest.Config,
    suite_key: str,
    suite_path: str,
    *,
    prefer_fresh: bool,
) -> tuple[RevitBridge | None, ConnectionError | None]:
    version = _opt_int(config, OPT_VERSION, OPT_VERSION)
    _ = _opt_bool(config, OPT_LAUNCH, OPT_LAUNCH)  # Keep option for backward compatibility.
    instances = _instances_for_version(version)

    lease = _lease_store.get_suite_lease(suite_key)
    if lease is not None:
        if not _is_process_alive(lease.process_id):
            _lease_store.clear_suite(suite_key)
            instances = _instances_for_version(version)
        else:
            leased_instance = _find_instance_by_pid(instances, lease.process_id)
            if leased_instance is None:
                _lease_store.clear_suite(suite_key)
                instances = _instances_for_version(version)
            else:
                bridge, connect_error = _connect_first_available([leased_instance])
                if bridge is not None:
                    _lease_store.assign(suite_key, suite_path, leased_instance)
                    print(
                        f"{PLUGIN_NAME}: Reusing lease suite={suite_key} pid={leased_instance.process_id} pipe={leased_instance.pipe_name}"
                    )
                    return bridge, None
                _lease_store.clear_suite(suite_key)
                instances = _instances_for_version(version)

    if not prefer_fresh:
        free_instances = _lease_store.find_free(suite_key, instances)
        bridge, selected, connect_error = _connect_first_available_with_instance(free_instances)
        if bridge is not None and selected is not None:
            _lease_store.assign(suite_key, suite_path, selected)
            print(
                f"{PLUGIN_NAME}: Assigned free instance suite={suite_key} pid={selected.process_id} pipe={selected.pipe_name}"
            )
            return bridge, None
        if connect_error is not None:
            return None, connect_error

    launch_version = _resolve_launch_version(version, instances)
    launched = _auto_launch(launch_version, config)
    bridge, selected, connect_error = _connect_first_available_with_instance([launched])
    if bridge is not None and selected is not None:
        _lease_store.assign(suite_key, suite_path, selected)
        print(
            f"{PLUGIN_NAME}: Spawned and leased suite={suite_key} pid={selected.process_id} pipe={selected.pipe_name}"
        )
        return bridge, None

    return None, connect_error


def _resolve_launch_version(version: int | None, instances: list[RevitInstance]) -> int:
    if version is not None:
        return version
    if instances:
        return max(instances, key=lambda instance: instance.version).version
    pytest.exit(
        f"{PLUGIN_NAME}: --revit-version is required when no existing instances are available.",
        returncode=EXIT_CODE_CONFIG_ERROR,
    )


def _find_instance_by_pid(instances: list[RevitInstance], process_id: int) -> RevitInstance | None:
    for instance in instances:
        if instance.process_id == process_id:
            return instance
    return None


def _is_process_alive(process_id: int) -> bool:
    process_query_limited_information = 0x1000
    handle = ctypes.windll.kernel32.OpenProcess(  # type: ignore[attr-defined]
        process_query_limited_information,
        False,
        int(process_id),
    )
    if handle == 0:
        return False
    ctypes.windll.kernel32.CloseHandle(handle)  # type: ignore[attr-defined]
    return True


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


def _connect_first_available_with_instance(
    instances: list[RevitInstance],
) -> tuple[RevitBridge | None, RevitInstance | None, ConnectionError | None]:
    last_error: ConnectionError | None = None
    for instance in instances:
        try:
            return _connect_pipe(instance.pipe_name), instance, None
        except ConnectionError as exc:
            last_error = exc
    return None, None, last_error


def _connect_pipe(pipe_name: str) -> RevitBridge:
    bridge = RevitBridge(pipe_name)
    try:
        bridge.connect()
        return bridge
    except ConnectionError:
        bridge.disconnect()
        raise


def _emit_item_reports(
    item: pytest.Item,
    results: list[CaseResult],
    *,
    collection_failed: bool,
    collection_error_message: str | None,
) -> list[pytest.TestReport]:
    ihook = item.ihook
    ihook.pytest_runtest_logstart(nodeid=item.nodeid, location=item.location)

    emitted: list[pytest.TestReport] = []
    if collection_failed:
        message = collection_error_message or "Remote collection failed before test execution."
        report = _make_error_report(item, message)
        ihook.pytest_runtest_logreport(report=report)
        emitted.append(report)
        ihook.pytest_runtest_logfinish(nodeid=item.nodeid, location=item.location)
        return emitted

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
