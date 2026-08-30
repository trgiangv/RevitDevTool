# coding: utf-8  # noqa: UP009
"""IronPython 2.7 / 3.4 test driver (unittest). No f-strings, no pytest."""

import json
import os
import sys
import time
import traceback
import unittest

try:
    import imp  # pyright: ignore[reportMissingImports]

    def _load_source(name, path):
        return imp.load_source(name, path)
except ImportError:
    import importlib.machinery

    def _load_source(name, path):
        return importlib.machinery.SourceFileLoader(name, path).load_module(name)

try:
    from StringIO import StringIO  # pyright: ignore[reportMissingImports]
except ImportError:
    from io import StringIO


def _engine_label():
    try:
        import pyrevit  # noqa: F401
        return "pyrevit"
    except Exception:  # noqa: BLE001
        return "embedded"


def _exc_text(err):
    return "".join(traceback.format_exception(err[0], err[1], err[2]))


def _nodeid(prefix, test):
    tid = test.id()
    parts = tid.split(".")
    if len(parts) >= 2:
        return prefix + "::" + parts[-2] + "::" + parts[-1]
    return prefix + "::" + tid


def _matches(nodeid, selected):
    if not selected:
        return True
    for item in selected:
        if item == nodeid:
            return True
        if "::" not in item and nodeid.startswith(item):
            return True
    return False


class JsonTestResult(unittest.TestResult):
    def __init__(self, prefix, selected):
        unittest.TestResult.__init__(self)
        self.prefix = prefix
        self.selected = selected
        self.records = []
        self._t0 = 0.0
        self._out = None
        self._err = None
        self._save_out = None
        self._save_err = None

    def startTest(self, test):
        unittest.TestResult.startTest(self, test)
        self._t0 = time.time()
        self._out = StringIO()
        self._err = StringIO()
        self._save_out = sys.stdout
        self._save_err = sys.stderr
        sys.stdout = self._out
        sys.stderr = self._err

    def stopTest(self, test):
        sys.stdout = self._save_out
        sys.stderr = self._save_err
        unittest.TestResult.stopTest(self, test)

    def _record(self, test, outcome, message, tb):
        nodeid = _nodeid(self.prefix, test)
        if not _matches(nodeid, self.selected):
            return
        duration_ms = (time.time() - self._t0) * 1000.0
        stdout = self._out.getvalue() if self._out is not None else ""
        stderr = self._err.getvalue() if self._err is not None else ""
        self.records.append({
            "nodeid": nodeid,
            "outcome": outcome,
            "phase": "call",
            "duration_ms": duration_ms,
            "stdout": stdout,
            "stderr": stderr,
            "message": message,
            "traceback": tb,
        })

    def addSuccess(self, test):
        unittest.TestResult.addSuccess(self, test)
        self._record(test, "passed", "", "")

    def addFailure(self, test, err):
        unittest.TestResult.addFailure(self, test, err)
        self._record(test, "failed", str(err[1]), _exc_text(err))

    def addError(self, test, err):
        unittest.TestResult.addError(self, test, err)
        self._record(test, "error", str(err[1]), _exc_text(err))

    def addSkip(self, test, reason):
        unittest.TestResult.addSkip(self, test, reason)
        self._record(test, "skipped", reason, "")


def _add_import_roots(test_path, workspace_root):
    """Put the test dir and ancestors up to workspace on sys.path (2.7-safe)."""
    test_dir = os.path.abspath(os.path.dirname(test_path))
    roots = [test_dir]
    ws = os.path.abspath(workspace_root) if workspace_root else None
    cur = test_dir
    while ws:
        parent = os.path.dirname(cur)
        if parent == cur:
            break
        roots.append(parent)
        if os.path.normcase(parent) == os.path.normcase(ws):
            break
        cur = parent
    for root in roots:
        if root not in sys.path:
            sys.path.insert(0, root)


def _run():
    here = os.path.dirname(os.path.abspath(__file__))
    request_path = os.environ.get("IPYTEST_REQUEST") or os.path.join(here, "request.json")
    payload = {
        "engine": _engine_label(),
        "results": [],
        "collection_errors": [],
    }
    result_path = ""
    try:
        with open(request_path, "r") as req:
            request = json.loads(req.read())
        result_path = request.get("result_path") or ""
        test_path = request.get("test_path") or ""
        workspace_root = request.get("workspace_root") or ""
        prefix = request.get("nodeid_prefix") or test_path.replace("\\", "/")
        selected = request.get("selected") or []
        if len(selected) == 1 and "::" not in selected[0]:
            selected = []

        _add_import_roots(test_path, workspace_root)

        module = _load_source("_ipy_under_test", test_path)
        suite = unittest.TestLoader().loadTestsFromModule(module)
        result = JsonTestResult(prefix, selected)
        suite.run(result)
        if not result.records:
            payload["collection_errors"].append({
                "nodeid": prefix,
                "path": test_path,
                "message": "No unittest.TestCase tests ran in this file.",
                "traceback": "",
            })
        payload["results"] = result.records
        payload["engine"] = _engine_label()
    except Exception:  # noqa: BLE001
        payload["collection_errors"].append({
            "nodeid": "",
            "path": request_path,
            "message": "IronPython test driver failed.",
            "traceback": traceback.format_exc(),
        })

    if result_path:
        with open(result_path, "w") as out:
            out.write(json.dumps(payload))


_run()
