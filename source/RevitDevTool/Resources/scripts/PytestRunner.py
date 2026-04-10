from __future__ import annotations

import json
import os
import site
import sys
import traceback
from typing import TYPE_CHECKING, Any

if TYPE_CHECKING:
    import pytest

_REQUEST_JSON = "__pytest_request_json__"
_RESULT_JSON = "__result_json__"
_EMPTY_SUMMARY = {
    "passed": 0,
    "failed": 0,
    "skipped": 0,
    "errors": 1,
    "xfailed": 0,
    "xpassed": 0,
}


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
        "nodeid",
        "outcome",
        "phase",
        "duration_ms",
        "stdout",
        "stderr",
        "message",
        "traceback",
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


class _BridgePlugin:
    def __init__(self) -> None:
        self.nodeids: list[str] = []
        self.results: list[_CaseResult] = []
        self.collection_errors: list[_CollectError] = []
        self.summary = {
            "passed": 0,
            "failed": 0,
            "skipped": 0,
            "errors": 0,
            "xfailed": 0,
            "xpassed": 0,
        }

    def pytest_collection_modifyitems(
        self, session: pytest.Session, config: pytest.Config, items: list[pytest.Item]
    ) -> None:
        _ = session, config
        self.nodeids = [item.nodeid for item in items]

    def pytest_collectreport(self, report: pytest.CollectReport) -> None:
        if report.failed:
            self.collection_errors.append(
                _CollectError(
                    nodeid=getattr(report, "nodeid", ""),
                    path=str(getattr(report, "fspath", "") or ""),
                    message=getattr(report, "longreprtext", "") or str(report.longrepr),
                    traceback=getattr(report, "longreprtext", "")
                    or str(report.longrepr),
                )
            )

    def pytest_runtest_logreport(self, report: pytest.TestReport) -> None:
        if report.when == "call":
            self.summary[self._summary_key(report)] += 1
        elif report.failed and report.when in {"setup", "teardown"}:
            self.summary["errors"] += 1

        self.results.append(
            _CaseResult(
                nodeid=report.nodeid,
                outcome=self._outcome(report),
                phase=report.when,
                duration_ms=float(report.duration or 0.0) * 1000.0,
                stdout=getattr(report, "capstdout", "") or "",
                stderr=getattr(report, "capstderr", "") or "",
                message=self._message(report),
                traceback=getattr(report, "longreprtext", "") or "",
            )
        )

    @staticmethod
    def _message(report: pytest.TestReport) -> str:
        if hasattr(report, "wasxfail"):
            return str(getattr(report, "wasxfail", "") or "")
        if getattr(report, "longreprtext", ""):
            return report.longreprtext.splitlines()[0]
        return ""

    @staticmethod
    def _outcome(report: pytest.TestReport) -> str:
        if report.passed and hasattr(report, "wasxfail"):
            return "xpassed"
        if report.skipped and hasattr(report, "wasxfail"):
            return "xfailed"
        if report.failed and report.when in {"setup", "teardown"}:
            return "error"
        return report.outcome

    @staticmethod
    def _summary_key(report: pytest.TestReport) -> str:
        outcome = _BridgePlugin._outcome(report)
        return (
            outcome
            if outcome in {"passed", "failed", "skipped", "xfailed", "xpassed"}
            else "errors"
        )


def _load_request() -> dict[str, Any]:
    payload = globals().get(_REQUEST_JSON, "")
    if not isinstance(payload, str) or not payload:
        raise RuntimeError("Pytest request payload is required.")
    request = json.loads(payload)
    if not isinstance(request, dict):
        raise RuntimeError("Pytest request payload must be a JSON object.")
    return request


def _build_args(request: dict[str, Any]) -> list[str]:
    args: list[str] = ["-p", "no:faulthandler", "--disable-plugin-autoload"]
    test_root = request.get("test_root") or ""
    if not isinstance(test_root, str) or not test_root:
        raise RuntimeError("test_root is required.")

    args.extend(
        [
            str(arg)
            for arg in request.get("pytest_args", [])
            if isinstance(arg, str) and arg
        ]
    )
    args.extend(
        [
            str(nodeid)
            for nodeid in request.get("nodeids", [])
            if isinstance(nodeid, str) and nodeid
        ]
    )

    if request.get("discover_only"):
        args.append("--collect-only")

    if not request.get("nodeids"):
        args.append(test_root)

    return args


def _prepare_paths(request: dict[str, Any]) -> str:
    workspace_root = (
        request.get("workspace_root") or request.get("test_root") or os.getcwd()
    )
    test_root = request.get("test_root") or workspace_root
    workspace_root = os.path.abspath(str(workspace_root))
    test_root = os.path.abspath(str(test_root))

    os.chdir(workspace_root)
    if workspace_root not in sys.path:
        sys.path.insert(0, workspace_root)
    if test_root not in sys.path:
        sys.path.insert(0, test_root)
    return test_root


def _patch_streams() -> None:
    for stream in (sys.stdout, sys.stderr):
        if not hasattr(stream, "isatty"):
            stream.isatty = lambda: False  # type: ignore[attr-defined]
        if not hasattr(stream, "fileno"):
            stream.fileno = lambda: -1  # type: ignore[attr-defined]


def _error_response(test_root: str, ex: Exception) -> str:
    return json.dumps(
        {
            "exit_code": 1,
            "summary": _EMPTY_SUMMARY,
            "results": [],
            "collection_errors": [
                {
                    "nodeid": "",
                    "path": test_root,
                    "message": str(ex),
                    "traceback": traceback.format_exc(),
                }
            ],
            "rootdir": test_root,
        }
    )


def _run() -> str:
    request = _load_request()
    test_root = _prepare_paths(request)
    _patch_streams()

    import pytest

    plugin = _BridgePlugin()
    args = _build_args(request)

    try:
        exit_code = pytest.main(args, plugins=[plugin])
    except Exception as ex:  # noqa: BLE001
        return _error_response(test_root, ex)
    results = [result.to_dict() for result in plugin.results]
    collection_errors = [error.to_dict() for error in plugin.collection_errors]

    return json.dumps(
        {
            "exit_code": int(exit_code),
            "summary": plugin.summary,
            "results": results,
            "collection_errors": collection_errors,
            "rootdir": test_root,
        }
    )


globals()[_RESULT_JSON] = _run()
