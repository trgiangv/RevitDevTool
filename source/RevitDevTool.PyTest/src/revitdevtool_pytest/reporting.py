"""Map remote host test results to pytest reports.

Pure functions — no module state, no globals. Every function receives
what it needs as explicit parameters.
"""

from __future__ import annotations

import json
import logging
import os
from typing import Any, Literal

import pytest

from .bridge import HostBridge
from .constants import (
    BRIDGE_NOTIFY_TEST_PROGRESS,
    NODEID_SEP,
    OUTCOME_ERROR,
    OUTCOME_FAILED,
    OUTCOME_PASSED,
    OUTCOME_SKIPPED,
    OUTCOME_XFAILED,
    OUTCOME_XPASSED,
    PHASE_CALL,
    PLUGIN_NAME,
    SUITE_ITEM_NAME,
)
from .ipy_collect import IpyTestItem
from .models import CaseResult, RunResponse

log = logging.getLogger(PLUGIN_NAME)

PytestOutcome = Literal["passed", "failed", "skipped"]

_VALID_REPORT_OUTCOMES = frozenset({OUTCOME_PASSED, OUTCOME_FAILED, OUTCOME_SKIPPED})


def host_pytest_args(config: pytest.Config) -> list[str]:
    """Forward local ``-x`` / ``--maxfail`` to the host (CPython and IronPython)."""
    maxfail = int(getattr(config.option, "maxfail", 0) or 0)
    if maxfail:
        return [f"--maxfail={maxfail}"]
    return []


def pipe_wait_timeout_s(
    timeout_per_test: float,
    item_count: int,
    *,
    ipy: bool,
    prepare_timeout_s: float = 0.0,
) -> float:
    """Named-pipe wait for one remote run.

    CPython ``tests/run`` includes host PEP 723 prepare, so *prepare_timeout_s*
    is added. IronPython ``ipytests/run`` never installs deps — run budget only.
    """
    run_budget = timeout_per_test * max(item_count, 1)
    if ipy:
        return run_budget
    return run_budget + prepare_timeout_s


def fan_suite_results(
    items: list[pytest.Item],
    results_by_nodeid: dict[str, list[CaseResult]],
) -> dict[str, list[CaseResult]]:
    """Attach host child nodeid results onto local suite collect items."""
    remapped = dict(results_by_nodeid)
    for item in items:
        if not isinstance(item, IpyTestItem) or item.name != SUITE_ITEM_NAME:
            continue
        prefix = item.nodeid.removesuffix(SUITE_ITEM_NAME)
        if not prefix.endswith(NODEID_SEP):
            continue
        fanned: list[CaseResult] = list(remapped.get(item.nodeid, []))
        keys_to_remove: list[str] = []
        for nodeid, results in remapped.items():
            if nodeid == item.nodeid:
                continue
            if nodeid.startswith(prefix):
                fanned.extend(results)
                keys_to_remove.append(nodeid)
        for key in keys_to_remove:
            del remapped[key]
        if fanned:
            remapped[item.nodeid] = fanned
    return remapped


# ---------------------------------------------------------------------------
# Remote session execution
# ---------------------------------------------------------------------------


def run_remote_session(
    session: pytest.Session,
    bridge: HostBridge,
    timeout_per_test: float,
    *,
    items: list[pytest.Item] | None = None,
    ipy: bool = False,
    prepare_timeout_s: float = 0.0,
    pytest_args: list[str] | None = None,
) -> tuple[dict[str, list[CaseResult]], set[str], bool, str | None]:
    """Run tests remotely, streaming progress as it arrives.

    Returns ``(results_by_nodeid, streamed_nodeids, collection_failed, error_msg)``.
    When an IDE adapter is active, streaming is disabled to avoid
    duplicate reports in the test tree.

    *prepare_timeout_s* is added to the pipe wait for CPython ``tests/run``
    (PEP 723 / pixi). Pass ``0`` for IronPython — ``ipytests/run`` does not
    install packages.
    """
    selected = items if items is not None else list(session.items)
    workspace_root = str(session.config.rootdir)
    total_timeout = pipe_wait_timeout_s(
        timeout_per_test, len(selected), ipy=ipy, prepare_timeout_s=prepare_timeout_s,
    )
    nodeids = [item.nodeid for item in selected]
    streamed: set[str] = set()

    on_notification = None if ipy else _build_streaming_callback(selected, streamed)
    response = _request_remote_run(
        bridge,
        workspace_root,
        nodeids,
        total_timeout,
        on_notification,
        ipy=ipy,
        pytest_args=pytest_args or [],
    )
    if response is None:
        return {}, streamed, True, f"{PLUGIN_NAME}: Remote execution failed."

    if ipy and response.engine:
        log.info("IronPython test engine: %s", response.engine)

    collection_error_message = _report_collection_errors(session, response)
    if _is_global_collection_failure(response):
        return {}, streamed, True, collection_error_message

    results_by_nodeid: dict[str, list[CaseResult]] = {}
    for r in response.results:
        results_by_nodeid.setdefault(r.nodeid, []).append(r)

    return results_by_nodeid, streamed, False, None


def _build_streaming_callback(
    items: list[pytest.Item],
    streamed: set[str],
) -> Any:
    """Build the notification callback for real-time streaming.

    Streaming is disabled when running under an IDE test adapter
    (VS Code / Cursor) because the adapter already processes the
    batch results and duplicate ``logreport`` events would cause
    double-counted results in the test tree.
    """
    if items and _is_ide_adapter_active(items[0].session):
        return None

    items_by_nodeid = {item.nodeid: item for item in items}

    def on_notification(method: str, params: Any) -> None:
        if method != BRIDGE_NOTIFY_TEST_PROGRESS or params is None:
            return
        _emit_streaming_report(params, items_by_nodeid, streamed)

    return on_notification


def _is_ide_adapter_active(session: pytest.Session) -> bool:
    """Detect if an IDE test adapter plugin is loaded."""
    pm = session.config.pluginmanager
    return pm.hasplugin("vscode_pytest") or bool(os.environ.get("TEST_RUN_PIPE"))


def _request_remote_run(
    bridge: HostBridge,
    workspace_root: str,
    nodeids: list[str],
    timeout_s: float,
    on_notification: Any,
    *,
    ipy: bool = False,
    pytest_args: list[str] | None = None,
) -> RunResponse | None:
    try:
        if ipy:
            return bridge.run_ipy_tests(
                workspace_root=workspace_root,
                test_root=workspace_root,
                nodeids=nodeids,
                pytest_args=pytest_args or [],
                timeout_s=timeout_s,
                on_notification=on_notification,
            )
        return bridge.run_tests(
            workspace_root=workspace_root,
            test_root=workspace_root,
            nodeids=nodeids,
            pytest_args=pytest_args or [],
            timeout_s=timeout_s,
            on_notification=on_notification,
        )
    except Exception as exc:  # noqa: BLE001
        log.error("Remote request failed: %s", exc)
        return None


def _report_collection_errors(
    session: pytest.Session,
    response: RunResponse,
) -> str | None:
    first_message: str | None = None
    for err in response.collection_errors:
        report_collection_error(session, err.nodeid, err.message, err.traceback)
        if first_message is None:
            first_message = err.message or err.traceback or "Remote collection failed."
    return first_message


# ---------------------------------------------------------------------------
# Streaming progress (real-time per-test reports)
# ---------------------------------------------------------------------------


def _emit_streaming_report(
    params: Any,
    items_by_nodeid: dict[str, pytest.Item],
    streamed: set[str],
) -> None:
    """Emit a live TestReport from a progress notification (CLI only).

    Emits ``logstart`` / ``logreport`` / ``logfinish`` so the terminal
    reporter can display results as they arrive. Items emitted here are
    tracked in *streamed* so the batch phase can skip them.
    """
    try:
        data = json.loads(params) if isinstance(params, str) else params
        if not isinstance(data, dict):
            return

        result = CaseResult.from_dict(data)
        item = items_by_nodeid.get(result.nodeid)
        if item is None:
            return

        ihook = item.ihook
        is_first = result.nodeid not in streamed
        if is_first:
            ihook.pytest_runtest_logstart(nodeid=item.nodeid, location=item.location)

        report = make_report(item, result)
        ihook.pytest_runtest_logreport(report=report)
        streamed.add(result.nodeid)

        if result.phase in {PHASE_CALL, "teardown"} or result.outcome in {
            OUTCOME_FAILED,
            OUTCOME_ERROR,
        }:
            ihook.pytest_runtest_logfinish(nodeid=item.nodeid, location=item.location)
    except Exception:  # noqa
        log.debug("Failed to emit streaming report", exc_info=True)


# ---------------------------------------------------------------------------
# Batch report emission (after full response)
# ---------------------------------------------------------------------------


def emit_item_reports(
    item: pytest.Item,
    results: list[CaseResult],
    *,
    collection_failed: bool,
    collection_error_message: str | None,
) -> list[pytest.TestReport]:
    ihook = item.ihook
    ihook.pytest_runtest_logstart(nodeid=item.nodeid, location=item.location)

    if collection_failed:
        message = collection_error_message or "Remote collection failed before test execution."
        reports = [_emit_single(ihook, make_error_report(item, message))]
    elif not results:
        reports = [_emit_single(ihook, make_error_report(item, "No result received from host for this test."))]
    else:
        reports = [_emit_single(ihook, make_report(item, r)) for r in results]

    ihook.pytest_runtest_logfinish(nodeid=item.nodeid, location=item.location)
    return reports


def _emit_single(ihook: Any, report: pytest.TestReport) -> pytest.TestReport:
    ihook.pytest_runtest_logreport(report=report)
    return report


# ---------------------------------------------------------------------------
# Report builders
# ---------------------------------------------------------------------------


def make_report(item: pytest.Item, result: CaseResult) -> pytest.TestReport:
    outcome, wasxfail = _normalize_outcome(result)
    longrepr = _build_longrepr(outcome, result)

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


def make_error_report(item: pytest.Item, message: str) -> pytest.TestReport:
    return pytest.TestReport(
        nodeid=item.nodeid,
        location=item.location,
        keywords=dict(item.keywords),
        outcome=OUTCOME_FAILED,  # type: ignore[arg-type]
        longrepr=message,  # type: ignore[arg-type]
        when=PHASE_CALL,  # type: ignore[arg-type]
    )


def _normalize_outcome(result: CaseResult) -> tuple[PytestOutcome, str]:
    outcome = result.outcome
    if outcome == OUTCOME_XFAILED:
        return OUTCOME_SKIPPED, result.message or "expected failure"  # type: ignore[return-value]
    if outcome == OUTCOME_XPASSED:
        return OUTCOME_FAILED, result.message or "expected failure but passed"  # type: ignore[return-value]
    if outcome == OUTCOME_ERROR or outcome not in _VALID_REPORT_OUTCOMES:
        return OUTCOME_FAILED, ""  # type: ignore[return-value]
    return outcome, ""  # type: ignore[return-value]


def _build_longrepr(
    outcome: str,
    result: CaseResult,
) -> str | tuple[str, int, str] | None:
    if outcome == OUTCOME_FAILED and (result.message or result.traceback):
        return result.traceback if result.traceback else result.message
    if outcome == OUTCOME_SKIPPED:
        return "", -1, f"Skipped: {result.message}" if result.message else "Skipped"
    return None


# ---------------------------------------------------------------------------
# Bulk status helpers
# ---------------------------------------------------------------------------


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
