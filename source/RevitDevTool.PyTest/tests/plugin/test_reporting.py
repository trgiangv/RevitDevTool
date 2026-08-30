from pathlib import Path
from unittest.mock import MagicMock

import pytest
from _pytest.outcomes import Exit

from revitdevtool_pytest.constants import DEFAULT_LAUNCH_TIMEOUT_S
from revitdevtool_pytest.models import CaseResult, RunResponse
from revitdevtool_pytest.reporting import (
    emit_item_reports,
    host_pytest_args,
    make_report,
    pipe_wait_timeout_s,
    run_remote_session,
)
from revitdevtool_pytest.suite_lock import host_lease_key, resolve_suite_context


def test_make_report_wires_stdout_section():
    item = MagicMock()
    item.nodeid = "tests/Revit_Ipy/test_document_ipy.py::TestActiveDocument::test_title"
    item.location = ("test_document_ipy.py", 13, "test_title")
    item.keywords = {}
    result = CaseResult(
        nodeid=item.nodeid,
        outcome="passed",
        phase="call",
        duration_ms=10.0,
        stdout="Project1\n",
        stderr="",
    )
    report = make_report(item, result)
    assert ("Captured stdout", "Project1\n") in report.sections
    assert report.capstdout == "Project1\n"
    assert report.capstderr == ""


def test_host_lease_key_shared_across_conftest_trees(tmp_path: Path):
    root = str(tmp_path)
    assert host_lease_key("revit", "2025", root) == host_lease_key("revit", "2025", root)
    assert host_lease_key("revit", "2025", root) != host_lease_key("civil3d", "2025", root)
    assert host_lease_key("revit", "2025", root) != host_lease_key("revit", "2026", root)


def test_resolve_suite_context_uses_workspace_not_conftest(tmp_path: Path):
    suite = tmp_path / "tests" / "Revit"
    suite.mkdir(parents=True)
    (suite / "conftest.py").write_text("")
    test_file = suite / "test_a.py"
    test_file.write_text("def test_a(): pass\n")

    session = MagicMock()
    session.config.rootpath = tmp_path
    item = MagicMock()
    item.path = test_file
    session.items = [item]

    key, suite_path = resolve_suite_context(session, "revit", "2025")
    assert suite_path == str(tmp_path.resolve())
    assert key == host_lease_key("revit", "2025", str(tmp_path))


def test_resolve_suite_context_rejects_mixed_conftest(tmp_path: Path):
    left = tmp_path / "Revit"
    right = tmp_path / "Revit_Ipy"
    left.mkdir()
    right.mkdir()
    (left / "conftest.py").write_text("")
    (right / "conftest.py").write_text("")
    left_test = left / "test_a.py"
    right_test = right / "test_b.py"
    left_test.write_text("def test_a(): pass\n")
    right_test.write_text("def test_b(): pass\n")

    session = MagicMock()
    session.config.rootpath = tmp_path
    item_a = MagicMock()
    item_a.path = left_test
    item_b = MagicMock()
    item_b.path = right_test
    session.items = [item_a, item_b]

    with pytest.raises(Exit, match="one conftest.py suite"):
        resolve_suite_context(session, "revit", "2025")


def test_make_report_skipped_setup():
    item = MagicMock()
    item.nodeid = "tests/Revit/test_a.py::test_a"
    item.location = ("test_a.py", 1, "test_a")
    item.keywords = {}
    result = CaseResult(
        nodeid=item.nodeid,
        outcome="skipped",
        phase="setup",
        message="fixture unavailable",
    )
    report = make_report(item, result)
    assert report.outcome == "skipped"
    assert report.when == "setup"
    assert not report.failed


def test_emit_item_reports_skipped_setup_not_missing():
    item = MagicMock()
    item.nodeid = "tests/Revit/test_a.py::test_a"
    item.location = ("test_a.py", 1, "test_a")
    item.keywords = {}
    ihook = MagicMock()
    item.ihook = ihook
    results = [
        CaseResult(
            nodeid=item.nodeid,
            outcome="skipped",
            phase="setup",
            message="fixture unavailable",
        ),
    ]
    reports = emit_item_reports(
        item,
        results,
        collection_failed=False,
        collection_error_message=None,
    )
    assert len(reports) == 1
    assert reports[0].outcome == "skipped"
    assert reports[0].when == "setup"
    assert not reports[0].failed
    ihook.pytest_runtest_logreport.assert_called_once()
    logged = ihook.pytest_runtest_logreport.call_args.kwargs["report"]
    assert logged.outcome == "skipped"
    assert "No result received" not in str(logged.longrepr)


def test_run_response_from_dict_keeps_engine():
    response = RunResponse.from_dict({
        "exit_code": 0,
        "summary": {},
        "results": [],
        "collection_errors": [],
        "rootdir": "/tmp",
        "engine": "pyrevit",
    })
    assert response.engine == "pyrevit"


def test_run_remote_session_ipy_uses_batch_only():
    session = MagicMock()
    session.config.rootdir = "/workspace"
    session.items = []
    bridge = MagicMock()
    bridge.run_ipy_tests.return_value = RunResponse(exit_code=0, engine="pyrevit")

    results, streamed, failed, error = run_remote_session(
        session,
        bridge,
        60.0,
        items=[],
        ipy=True,
        pytest_args=["--maxfail=1"],
    )

    bridge.run_ipy_tests.assert_called_once()
    call_kwargs = bridge.run_ipy_tests.call_args.kwargs
    assert call_kwargs["on_notification"] is None
    assert call_kwargs["timeout_s"] == 60.0
    assert call_kwargs["pytest_args"] == ["--maxfail=1"]
    assert results == {}
    assert streamed == set()
    assert not failed
    assert error is None


def test_pipe_wait_timeout_cpython_adds_prepare_ipy_does_not():
    assert pipe_wait_timeout_s(60.0, 2, ipy=False, prepare_timeout_s=DEFAULT_LAUNCH_TIMEOUT_S) == (
        120.0 + DEFAULT_LAUNCH_TIMEOUT_S
    )
    assert pipe_wait_timeout_s(60.0, 2, ipy=True, prepare_timeout_s=DEFAULT_LAUNCH_TIMEOUT_S) == 120.0
    assert pipe_wait_timeout_s(60.0, 0, ipy=True) == 60.0


def test_run_remote_session_cpython_pipe_wait_includes_prepare():
    session = MagicMock()
    session.config.rootdir = "/workspace"
    item = MagicMock()
    item.nodeid = "tests/test_a.py::test_a"
    session.items = [item]
    bridge = MagicMock()
    bridge.run_tests.return_value = RunResponse(exit_code=0)

    run_remote_session(
        session,
        bridge,
        60.0,
        items=[item],
        prepare_timeout_s=DEFAULT_LAUNCH_TIMEOUT_S,
        pytest_args=["--maxfail=1"],
    )

    assert bridge.run_tests.call_args.kwargs["timeout_s"] == 60.0 + DEFAULT_LAUNCH_TIMEOUT_S
    assert bridge.run_tests.call_args.kwargs["pytest_args"] == ["--maxfail=1"]
    bridge.run_ipy_tests.assert_not_called()


def test_host_pytest_args_forwards_maxfail_not_ipy():
    config = MagicMock()
    config.option.maxfail = 2
    assert host_pytest_args(config) == ["--maxfail=2"]
    config.option.maxfail = 0
    assert host_pytest_args(config) == []
    config.option.maxfail = None
    assert host_pytest_args(config) == []
