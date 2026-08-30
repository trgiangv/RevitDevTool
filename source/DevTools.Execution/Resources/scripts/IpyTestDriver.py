# coding: utf-8  # noqa: UP009
"""IronPython 2.7 / 3.4 test driver (unittest). No f-strings, no pytest.

Do not assign to the name ``print`` — it is a keyword on 2.7 and will not parse.
Capture by teeing ``sys.stdout`` / ``sys.stderr`` (pyRevit ScriptIO + case result).
"""

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
    import pyrevit  # noqa: F401
    _IS_PYREVIT = True
except ImportError:
    _IS_PYREVIT = False

_NODEID_SEP = "::"
_SUITE_ITEM = "(suite)"
_SUITE_SELECTOR = _NODEID_SEP + _SUITE_ITEM
_PHASE_CALL = "call"


def _engine_label():
    if _IS_PYREVIT:
        return "pyrevit"
    return "embedded"


_did_close_others = False


def _close_pyrevit_outputs():
    """Keep the current pyRevit Output window; close leftovers from prior files."""
    global _did_close_others
    if not _IS_PYREVIT or _did_close_others:
        return
    _did_close_others = True
    from pyrevit import script
    script.get_output().close_others(True)


def _exc_text(err):
    return "".join(traceback.format_exception(err[0], err[1], err[2]))


def _nodeid(prefix, test):
    tid = test.id()
    parts = tid.split(".")
    if len(parts) >= 2:
        return _NODEID_SEP.join([prefix, parts[-2], parts[-1]])
    return _NODEID_SEP.join([prefix, tid])


def _matches(nodeid, selected):
    if not selected:
        return True
    for item in selected:
        if item.endswith(_SUITE_SELECTOR):
            file_part = item[: -len(_SUITE_SELECTOR)]
            if nodeid == file_part or nodeid.startswith(file_part + _NODEID_SEP):
                return True
            continue
        if item == nodeid or nodeid.startswith(item + _NODEID_SEP):
            return True
    return False


class _Tee:
    def __init__(self, original, chunks):
        self._original = original
        self._chunks = chunks

    def write(self, data):
        if data:
            _close_pyrevit_outputs()
            self._chunks.append(str(data))
        self._original.write(data)

    def flush(self):
        self._original.flush()


class JsonTestResult(unittest.TestResult):
    def __init__(self, prefix, maxfail=0):
        unittest.TestResult.__init__(self)
        self.prefix = prefix
        self.records = []
        self._maxfail = maxfail if maxfail else 0
        self._t0 = 0.0
        self._out_chunks = []
        self._err_chunks = []
        self._save_out = None
        self._save_err = None

    def startTest(self, test):
        unittest.TestResult.startTest(self, test)
        self._t0 = time.time()
        self._out_chunks = []
        self._err_chunks = []
        self._save_out = sys.stdout
        self._save_err = sys.stderr
        sys.stdout = _Tee(self._save_out, self._out_chunks)
        sys.stderr = _Tee(self._save_err, self._err_chunks)

    def stopTest(self, test):
        sys.stdout = self._save_out
        sys.stderr = self._save_err
        unittest.TestResult.stopTest(self, test)

    def _record(self, test, outcome, message, tb):
        self.records.append({
            "nodeid": _nodeid(self.prefix, test),
            "outcome": outcome,
            "phase": _PHASE_CALL,
            "duration_ms": (time.time() - self._t0) * 1000.0,
            "stdout": "".join(self._out_chunks),
            "stderr": "".join(self._err_chunks),
            "message": message,
            "traceback": tb,
        })

    def _check_maxfail(self):
        if self._maxfail > 0 and len(self.failures) + len(self.errors) >= self._maxfail:
            self.shouldStop = True

    def addSuccess(self, test):
        unittest.TestResult.addSuccess(self, test)
        self._record(test, "passed", "", "")

    def addFailure(self, test, err):
        unittest.TestResult.addFailure(self, test, err)
        self._record(test, "failed", str(err[1]), _exc_text(err))
        self._check_maxfail()

    def addError(self, test, err):
        unittest.TestResult.addError(self, test, err)
        self._record(test, "error", str(err[1]), _exc_text(err))
        self._check_maxfail()

    def addSkip(self, test, reason):
        unittest.TestResult.addSkip(self, test, reason)
        self._record(test, "skipped", reason, "")


def _iter_tests(suite):
    tests = []
    for test in suite:
        if isinstance(test, unittest.TestSuite):
            tests.extend(_iter_tests(test))
        else:
            tests.append(test)
    return tests


def _filter_suite(suite, prefix, selected):
    if not selected:
        return suite
    filtered = unittest.TestSuite()
    for test in _iter_tests(suite):
        if _matches(_nodeid(prefix, test), selected):
            filtered.addTest(test)
    return filtered


def _add_import_roots(test_path, workspace_root):
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
    inserted = []
    for root in roots:
        if root not in sys.path:
            sys.path.insert(0, root)
            inserted.append(root)
    return inserted


def _restore_import_roots(inserted):
    for path in reversed(inserted):
        try:
            sys.path.remove(path)
        except ValueError:
            pass


def _run():
    here = os.path.dirname(os.path.abspath(__file__))
    request_path = os.environ.get("IPYTEST_REQUEST") or os.path.join(here, "request.json")
    payload = {
        "engine": _engine_label(),
        "results": [],
        "collection_errors": [],
    }
    result_path = ""
    inserted = []
    try:
        with open(request_path, "r") as req:
            request = json.loads(req.read())
        result_path = request.get("result_path") or ""
        test_path = request.get("test_path") or ""
        workspace_root = request.get("workspace_root") or ""
        prefix = request.get("nodeid_prefix") or test_path.replace("\\", "/")
        selected = request.get("selected") or []

        inserted = _add_import_roots(test_path, workspace_root)
        module = _load_source("_ipy_under_test", test_path)
        suite = _filter_suite(
            unittest.TestLoader().loadTestsFromModule(module),
            prefix,
            selected,
        )
        maxfail = int(request.get("maxfail") or 0)
        result = JsonTestResult(prefix, maxfail)
        suite.run(result)
        if not result.records:
            payload["collection_errors"].append({
                "nodeid": prefix,
                "path": test_path,
                "message": "No unittest.TestCase tests ran in this file.",
                "traceback": "",
            })
        payload["results"] = result.records
    except Exception:  # noqa: BLE001
        payload["collection_errors"].append({
            "nodeid": "",
            "path": request_path,
            "message": "IronPython test driver failed.",
            "traceback": traceback.format_exc(),
        })
    finally:
        _restore_import_roots(inserted)

    if result_path:
        with open(result_path, "w") as out:
            out.write(json.dumps(payload))


_run()
