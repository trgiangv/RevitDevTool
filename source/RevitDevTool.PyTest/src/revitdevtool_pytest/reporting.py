"""Map remote Revit test results to pytest reports.

Pure functions — no module state, no globals. Every function receives
what it needs as explicit parameters.
"""

from __future__ import annotations

import logging
from typing import Literal

import pytest

from .bridge import RevitBridge
from .constants import (
    OUTCOME_ERROR,
    OUTCOME_FAILED,
    OUTCOME_PASSED,
    OUTCOME_SKIPPED,
    OUTCOME_XFAILED,
    OUTCOME_XPASSED,
    PHASE_CALL,
    PLUGIN_NAME,
)
from .models import CaseResult, RunResponse

log = logging.getLogger(PLUGIN_NAME)

PytestOutcome = Literal["passed", "failed", "skipped"]

_VALID_REPORT_OUTCOMES = frozenset({OUTCOME_PASSED, OUTCOME_FAILED, OUTCOME_SKIPPED})


def run_remote_session(
    session: pytest.Session,
    bridge: RevitBridge,
    timeout_per_test: float,
) -> tuple[dict[str, list[CaseResult]], bool, str | None]:
    workspace_root = str(session.config.rootdir)
    total_timeout = timeout_per_test * max(len(session.items), 1)
    nodeids = [item.nodeid for item in session.items]

    response = request_remote_run(bridge, workspace_root, nodeids, total_timeout)
    if response is None:
        fail_all(session, f"{PLUGIN_NAME}: Remote execution failed.")
        return {}, False, None

    collection_error_message: str | None = None
    for err in response.collection_errors:
        report_collection_error(session, err.nodeid, err.message, err.traceback)
        if collection_error_message is None:
            collection_error_message = (
                err.message or err.traceback or "Remote collection failed."
            )

    if _is_global_collection_failure(response):
        return {}, True, collection_error_message

    results_by_nodeid: dict[str, list[CaseResult]] = {}
    for r in response.results:
        results_by_nodeid.setdefault(r.nodeid, []).append(r)

    return results_by_nodeid, False, None


def request_remote_run(
    bridge: RevitBridge,
    workspace_root: str,
    nodeids: list[str],
    timeout_s: float,
) -> RunResponse | None:
    try:
        return bridge.run_tests(
            workspace_root=workspace_root,
            test_root=workspace_root,
            nodeids=nodeids,
            timeout_s=timeout_s,
        )
    except Exception as exc:  # noqa: BLE001
        log.error("Remote request failed: %s", exc)
        return None


def emit_item_reports(
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
        message = (
            collection_error_message
            or "Remote collection failed before test execution."
        )
        report = make_error_report(item, message)
        ihook.pytest_runtest_logreport(report=report)
        emitted.append(report)
        ihook.pytest_runtest_logfinish(nodeid=item.nodeid, location=item.location)
        return emitted

    if not results:
        report = make_error_report(
            item, "No result received from Revit for this test."
        )
        ihook.pytest_runtest_logreport(report=report)
        emitted.append(report)
    else:
        for r in results:
            report = make_report(item, r)
            ihook.pytest_runtest_logreport(report=report)
            emitted.append(report)

    ihook.pytest_runtest_logfinish(nodeid=item.nodeid, location=item.location)
    return emitted


def make_report(item: pytest.Item, result: CaseResult) -> pytest.TestReport:
    outcome, wasxfail = normalize_outcome(result)

    longrepr: str | tuple[str, int, str] | None = None
    if outcome == OUTCOME_FAILED and (result.message or result.traceback):
        longrepr = result.traceback if result.traceback else result.message
    elif outcome == OUTCOME_SKIPPED:
        longrepr = (
            "",
            -1,
            f"Skipped: {result.message}" if result.message else "Skipped",
        )

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
        longrepr=longrepr,  # type: ignore[arg-type]
        when=result.phase,  # type: ignore[arg-type]
        duration=result.duration_ms / 1000.0,
        sections=sections,
    )
    if wasxfail:
        report.wasxfail = wasxfail  # type: ignore[attr-defined]
    return report


def normalize_outcome(result: CaseResult) -> tuple[PytestOutcome, str]:
    """Map remote outcome to pytest-native outcome + optional wasxfail reason."""
    outcome = result.outcome
    if outcome == OUTCOME_XFAILED:
        return OUTCOME_SKIPPED, result.message or "expected failure"  # type: ignore[return-value]
    if outcome == OUTCOME_XPASSED:
        return OUTCOME_FAILED, result.message or "expected failure but passed"  # type: ignore[return-value]
    if outcome == OUTCOME_ERROR or outcome not in _VALID_REPORT_OUTCOMES:
        return OUTCOME_FAILED, ""  # type: ignore[return-value]
    return outcome, ""  # type: ignore[return-value]


def make_error_report(item: pytest.Item, message: str) -> pytest.TestReport:
    return pytest.TestReport(
        nodeid=item.nodeid,
        location=item.location,
        keywords=dict(item.keywords),
        outcome=OUTCOME_FAILED,  # type: ignore[arg-type]
        longrepr=message,  # type: ignore[arg-type]
        when=PHASE_CALL,  # type: ignore[arg-type]
    )


def skip_all(session: pytest.Session, reason: str) -> None:
    for item in session.items:
        ihook = item.ihook
        ihook.pytest_runtest_logstart(nodeid=item.nodeid, location=item.location)
        report = pytest.TestReport(
            nodeid=item.nodeid,
            location=item.location,
            keywords=dict(item.keywords),
            outcome=OUTCOME_SKIPPED,  # type: ignore[arg-type]
            longrepr=("", -1, f"Skipped: {reason}"),  # type: ignore[arg-type]
            when=PHASE_CALL,  # type: ignore[arg-type]
        )
        ihook.pytest_runtest_logreport(report=report)
        ihook.pytest_runtest_logfinish(nodeid=item.nodeid, location=item.location)


def fail_all(session: pytest.Session, message: str) -> None:
    for item in session.items:
        ihook = item.ihook
        ihook.pytest_runtest_logstart(nodeid=item.nodeid, location=item.location)
        ihook.pytest_runtest_logreport(report=make_error_report(item, message))
        ihook.pytest_runtest_logfinish(nodeid=item.nodeid, location=item.location)


def report_collection_error(
    session: pytest.Session, nodeid: str, message: str, tb: str,
) -> None:
    full = f"{message}\n{tb}" if tb else message
    session.config.hook.pytest_collectreport(
        report=pytest.CollectReport(
            nodeid=nodeid or "<collection>",
            outcome=OUTCOME_FAILED,  # type: ignore[arg-type]
            longrepr=full,  # type: ignore[arg-type]
            result=[],
        ),
    )


def _is_global_collection_failure(response: RunResponse) -> bool:
    return bool(response.collection_errors) and not response.results
