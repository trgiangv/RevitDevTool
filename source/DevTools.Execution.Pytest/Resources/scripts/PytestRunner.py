from __future__ import annotations

import json
import os
import sys
import traceback
from typing import TYPE_CHECKING, Any

if TYPE_CHECKING:
    import pytest

_REQUEST_JSON = "__pytest_request_json__"
_RESULT_JSON = "__result_json__"
_PROGRESS_CALLBACK = "__progress_callback__"
_LOG_FUNC = "__log_func__"

_PHASE_CALL = "call"
_PHASE_SETUP = "setup"
_PHASE_TEARDOWN = "teardown"

_OUTCOME_PASSED = "passed"
_OUTCOME_FAILED = "failed"
_OUTCOME_SKIPPED = "skipped"
_OUTCOME_ERROR = "error"
_OUTCOME_XFAILED = "xfailed"
_OUTCOME_XPASSED = "xpassed"
_OUTCOME_ERRORS = "errors"

_FAILURE_OUTCOMES = frozenset({_OUTCOME_FAILED, _OUTCOME_ERROR})
_SUMMARY_OUTCOMES = frozenset({
    _OUTCOME_PASSED, _OUTCOME_FAILED, _OUTCOME_SKIPPED,
    _OUTCOME_XFAILED, _OUTCOME_XPASSED,
})

_EMPTY_SUMMARY: dict[str, int] = {
    _OUTCOME_PASSED: 0,
    _OUTCOME_FAILED: 0,
    _OUTCOME_SKIPPED: 0,
    _OUTCOME_ERRORS: 1,
    _OUTCOME_XFAILED: 0,
    _OUTCOME_XPASSED: 0,
}


# ---------------------------------------------------------------------------
# Data containers
# ---------------------------------------------------------------------------


class _CollectError:
    __slots__ = ("nodeid", "path", "message", "traceback")

    def __init__(self, nodeid: str, path: str, message: str, traceback: str) -> None:
        self.nodeid = nodeid
        self.path = path
        self.message = message
        self.traceback = traceback

    def to_dict(self) -> dict[str, Any]:
        return {
            "nodeid": self.nodeid,
            "path": self.path,
            "message": self.message,
            "traceback": self.traceback,
        }


class _CaseResult:
    __slots__ = (
        "nodeid", "outcome", "phase", "duration_ms",
        "stdout", "stderr", "message", "traceback",
    )

    def __init__(
        self,
        nodeid: str,
        outcome: str,
        phase: str,
        duration_ms: float,
        stdout: str,
        stderr: str,
        message: str,
        traceback: str,
    ) -> None:
        self.nodeid = nodeid
        self.outcome = outcome
        self.phase = phase
        self.duration_ms = duration_ms
        self.stdout = stdout
        self.stderr = stderr
        self.message = message
        self.traceback = traceback

    def to_dict(self) -> dict[str, Any]:
        return {
            "nodeid": self.nodeid,
            "outcome": self.outcome,
            "phase": self.phase,
            "duration_ms": self.duration_ms,
            "stdout": self.stdout,
            "stderr": self.stderr,
            "message": self.message,
            "traceback": self.traceback,
        }


# ---------------------------------------------------------------------------
# Outcome helpers
# ---------------------------------------------------------------------------


def _extract_outcome(report: pytest.TestReport) -> str:
    if report.passed and hasattr(report, "wasxfail"):
        return _OUTCOME_XPASSED
    if report.skipped and hasattr(report, "wasxfail"):
        return _OUTCOME_XFAILED
    if report.failed and report.when in {_PHASE_SETUP, _PHASE_TEARDOWN}:
        return _OUTCOME_ERROR
    return report.outcome


def _extract_message(report: pytest.TestReport) -> str:
    if hasattr(report, "wasxfail"):
        return str(getattr(report, "wasxfail", "") or "")
    if getattr(report, "longreprtext", ""):
        return report.longreprtext.splitlines()[0]
    return ""


def _summary_key(report: pytest.TestReport) -> str:
    outcome = _extract_outcome(report)
    if outcome in _SUMMARY_OUTCOMES:
        return outcome
    return _OUTCOME_ERRORS


def _build_case_result(report: pytest.TestReport) -> _CaseResult:
    return _CaseResult(
        nodeid=report.nodeid,
        outcome=_extract_outcome(report),
        phase=report.when,
        duration_ms=float(report.duration or 0.0) * 1000.0,
        stdout=getattr(report, "capstdout", "") or "",
        stderr=getattr(report, "capstderr", "") or "",
        message=_extract_message(report),
        traceback=getattr(report, "longreprtext", "") or "",
    )


# ---------------------------------------------------------------------------
# pytest plugin
# ---------------------------------------------------------------------------


def _echo_to_log_viewer(result: _CaseResult) -> None:
    """Replay test result to Revit's Log Viewer via __log_func__."""
    import builtins

    log_func = getattr(builtins, _LOG_FUNC, None)
    if log_func is None:
        return

    if result.phase != _PHASE_CALL:
        if result.outcome in _FAILURE_OUTCOMES and result.traceback:
            log_func(f"[{result.nodeid}] {result.phase} FAILED:\n{result.traceback}")
        return

    try:
        log_func(f"[{result.nodeid}] {result.outcome.upper()} ({result.duration_ms:.0f}ms)")

        if result.stdout:
            log_func(result.stdout)
        if result.stderr:
            log_func(result.stderr)
        if result.traceback and result.outcome in _FAILURE_OUTCOMES:
            log_func(result.traceback)
    except Exception:  # noqa: BLE001
        pass


class _BridgePlugin:
    def __init__(self, progress_callback: Any | None = None) -> None:
        self.nodeids: list[str] = []
        self.results: list[_CaseResult] = []
        self.collection_errors: list[_CollectError] = []
        self.summary: dict[str, int] = {
            _OUTCOME_PASSED: 0, _OUTCOME_FAILED: 0, _OUTCOME_SKIPPED: 0,
            _OUTCOME_ERRORS: 0, _OUTCOME_XFAILED: 0, _OUTCOME_XPASSED: 0,
        }
        self._progress = progress_callback

    def pytest_collection_modifyitems(
        self, session: pytest.Session, config: pytest.Config, items: list[pytest.Item],
    ) -> None:
        _ = session, config
        self.nodeids = [item.nodeid for item in items]

    def pytest_collectreport(self, report: pytest.CollectReport) -> None:
        if not report.failed:
            return
        self.collection_errors.append(
            _CollectError(
                nodeid=getattr(report, "nodeid", ""),
                path=str(getattr(report, "fspath", "") or ""),
                message=getattr(report, "longreprtext", "") or str(report.longrepr),
                traceback=getattr(report, "longreprtext", "") or str(report.longrepr),
            )
        )

    def pytest_runtest_logreport(self, report: pytest.TestReport) -> None:
        if report.when == _PHASE_CALL:
            self.summary[_summary_key(report)] += 1
        elif report.failed and report.when in {_PHASE_SETUP, _PHASE_TEARDOWN}:
            self.summary[_OUTCOME_ERRORS] += 1

        result = _build_case_result(report)
        self.results.append(result)
        _echo_to_log_viewer(result)
        self._emit_progress(result)

    def _emit_progress(self, result: _CaseResult) -> None:
        if self._progress is None:
            return
        try:
            self._progress(json.dumps(result.to_dict(), ensure_ascii=False))
        except Exception:  # noqa: BLE001
            pass


# ---------------------------------------------------------------------------
# Request / arg parsing
# ---------------------------------------------------------------------------


def _load_request() -> dict[str, Any]:
    payload = globals().get(_REQUEST_JSON, "")
    if not isinstance(payload, str) or not payload:
        raise RuntimeError("Pytest request payload is required.")
    request = json.loads(payload)
    if not isinstance(request, dict):
        raise RuntimeError("Pytest request payload must be a JSON object.")
    return request


def _build_args(request: dict[str, Any]) -> list[str]:
    test_root = request.get("test_root") or ""
    if not isinstance(test_root, str) or not test_root:
        raise RuntimeError("test_root is required.")

    args: list[str] = [
        "-p", "no:faulthandler",
        "--disable-plugin-autoload",
        "--capture=sys",
        "-W", "ignore::pytest.PytestConfigWarning",
    ]
    args.extend(
        str(arg) for arg in request.get("pytest_args", [])
        if isinstance(arg, str) and arg
    )
    args.extend(
        str(nid) for nid in request.get("nodeids", [])
        if isinstance(nid, str) and nid
    )
    if request.get("discover_only"):
        args.append("--collect-only")
    if not request.get("nodeids"):
        args.append(test_root)
    return args


# ---------------------------------------------------------------------------
# Path setup (with cwd restore)
# ---------------------------------------------------------------------------


def _prepare_paths(request: dict[str, Any]) -> tuple[str, str]:
    """Configure sys.path for the test run.

    Returns ``(test_root, saved_cwd)`` so the caller can restore cwd later.
    """
    workspace_root = (
        request.get("workspace_root") or request.get("test_root") or os.getcwd()
    )
    test_root = request.get("test_root") or workspace_root
    workspace_root = os.path.abspath(str(workspace_root))
    test_root = os.path.abspath(str(test_root))

    saved_cwd = os.getcwd()
    os.chdir(workspace_root)
    if workspace_root not in sys.path:
        sys.path.insert(0, workspace_root)
    if test_root not in sys.path:
        sys.path.insert(0, test_root)
    return test_root, saved_cwd


def _enable_pytest_streams() -> tuple[Any, Any, Any]:
    """Set pytest-mode flag and replace Trace-backed streams with standard I/O.

    SetupRevit.py redirects builtins.print and sys.stdout/stderr to Trace.
    Setting sys.__pytest_running__ prevents re-hijacking during the run.
    Returns (saved_stdout, saved_stderr, saved_print) for restoration.
    """
    import builtins
    import io

    sys.__pytest_running__ = True  # type: ignore[attr-defined]

    saved_stdout = sys.stdout
    saved_stderr = sys.stderr
    saved_print = builtins.print

    def _real_print(*args: Any, sep: str = " ", end: str = "\n", file: Any = None, flush: bool = False) -> None:  # noqa: ANN401
        target = file if file is not None else sys.stdout
        text = sep.join(str(a) for a in args) + end
        target.write(text)
        if flush:
            target.flush()

    builtins.print = _real_print  # type: ignore[assignment]

    sys.stdout = io.StringIO()
    sys.stderr = io.StringIO()

    for stream in (sys.stdout, sys.stderr):
        if not hasattr(stream, "isatty"):
            stream.isatty = lambda: False  # type: ignore[attr-defined]
        if not hasattr(stream, "fileno"):
            stream.fileno = lambda: -1  # type: ignore[attr-defined]

    return saved_stdout, saved_stderr, saved_print


def _disable_pytest_streams(saved_stdout: Any, saved_stderr: Any, saved_print: Any) -> None:
    """Restore Trace-backed streams and clear pytest-mode flag."""
    import builtins

    sys.__pytest_running__ = False  # type: ignore[attr-defined]
    sys.stdout = saved_stdout
    sys.stderr = saved_stderr
    builtins.print = saved_print


# ---------------------------------------------------------------------------
# Response builders
# ---------------------------------------------------------------------------


def _success_response(
    exit_code: int,
    plugin: _BridgePlugin,
    test_root: str,
    *,
    discover_only: bool = False,
) -> str:
    if discover_only:
        return json.dumps({
            "rootdir": test_root,
            "nodeids": plugin.nodeids,
            "collection_errors": [e.to_dict() for e in plugin.collection_errors],
        })
    return json.dumps({
        "exit_code": int(exit_code),
        "summary": plugin.summary,
        "results": [r.to_dict() for r in plugin.results],
        "collection_errors": [e.to_dict() for e in plugin.collection_errors],
        "rootdir": test_root,
    })


def _error_response(test_root: str, ex: Exception, *, discover_only: bool = False) -> str:
    error_entry = {
        "nodeid": "",
        "path": test_root,
        "message": str(ex),
        "traceback": traceback.format_exc(),
    }
    if discover_only:
        return json.dumps({
            "rootdir": test_root,
            "nodeids": [],
            "collection_errors": [error_entry],
        })
    return json.dumps({
        "exit_code": 1,
        "summary": _EMPTY_SUMMARY,
        "results": [],
        "collection_errors": [error_entry],
        "rootdir": test_root,
    })


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------


def _run() -> str:
    request: dict[str, Any] = {}
    test_root = ""
    saved_cwd = os.getcwd()
    saved_streams: tuple[Any, Any, Any] | None = None
    try:
        request = _load_request()
        test_root, saved_cwd = _prepare_paths(request)
        saved_streams = _enable_pytest_streams()

        import pytest

        discover_only = bool(request.get("discover_only"))
        progress_callback = globals().get(_PROGRESS_CALLBACK)
        plugin = _BridgePlugin(progress_callback)
        args = _build_args(request)

        try:
            exit_code = pytest.main(args, plugins=[plugin])
        except Exception as ex:  # noqa: BLE001
            return _error_response(test_root, ex, discover_only=discover_only)

        return _success_response(exit_code, plugin, test_root, discover_only=discover_only)
    except Exception as ex:  # noqa: BLE001
        return _error_response(
            test_root or request.get("test_root", ""),
            ex,
            discover_only=bool(request.get("discover_only")),
        )
    finally:
        if saved_streams is not None:
            _disable_pytest_streams(*saved_streams)
        os.chdir(saved_cwd)


globals()[_RESULT_JSON] = _run()
