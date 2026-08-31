# coding: utf-8  # noqa: UP009
"""IronPython 2.7 / 3.4 unittest driver. Sidecar request_{pid}.json → result_{pid}.json."""

import json
import os
import sys
import time
import traceback
import unittest

try:
    import imp  # noqa  # pyright: ignore[reportMissingImports]

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

_ENGINE = "pyrevit" if _IS_PYREVIT else "embedded"
_SEP = "::"
_SUITE_SEL = _SEP + "(suite)"
_PHASE = "call"
_PASSED = "passed"
_FAILED = "failed"
_SKIPPED = "skipped"
_ERROR = "error"
_HOLDER = "_ErrorHolder"

_did_close_pyrevit = False


def _sidecar_paths(driver_dir):
    pid = os.getpid()
    return (
        os.path.join(driver_dir, "request_{}.json".format(pid)),  # noqa: UP032
        os.path.join(driver_dir, "result_{}.json".format(pid)),  # noqa: UP032
    )


def _payload(results, collection_errors):
    return {"engine": _ENGINE, "results": results, "collection_errors": collection_errors}


def _case(nodeid, outcome, duration_ms, stdout, stderr, message, tb):
    return {
        "nodeid": nodeid,
        "outcome": outcome,
        "phase": _PHASE,
        "duration_ms": duration_ms,
        "stdout": stdout,
        "stderr": stderr,
        "message": message,
        "traceback": tb,
    }


def _collect_err(nodeid, path, message, tb):
    return {"nodeid": nodeid, "path": path, "message": message, "traceback": tb}


def _nodeid(prefix, test):
    parts = test.id().split(".")
    if len(parts) >= 2:
        return _SEP.join([prefix, parts[-2], parts[-1]])
    return _SEP.join([prefix, test.id()])


def _holder_nodeid(prefix, holder):
    desc = getattr(holder, "description", "") or ""
    if "(" in desc and desc.endswith(")"):
        inner = desc[desc.index("(") + 1:-1]
        class_name = inner.rsplit(".", 1)[-1]
        if class_name:
            return _SEP.join([prefix, class_name])
    return prefix


def _matches(nodeid, selected):
    if not selected:
        return True
    for item in selected:
        if item.endswith(_SUITE_SEL):
            root = item[: -len(_SUITE_SEL)]
            if nodeid == root or nodeid.startswith(root + _SEP):
                return True
        elif item == nodeid or nodeid.startswith(item + _SEP):
            return True
    return False


def _close_pyrevit_outputs():
    global _did_close_pyrevit
    if not _IS_PYREVIT or _did_close_pyrevit:
        return
    _did_close_pyrevit = True
    from pyrevit import script

    script.get_output().close_others(True)


def _exc_text(err):
    return "".join(traceback.format_exception(err[0], err[1], err[2]))


class _Tee(object):  # noqa: UP004
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
        self.collection_errors = []
        self._maxfail = maxfail or 0
        self._t0 = 0.0
        self._out = []
        self._err = []
        self._save_out = None
        self._save_err = None

    def startTest(self, test):
        unittest.TestResult.startTest(self, test)
        self._t0 = time.time()
        self._out = []
        self._err = []
        self._save_out = sys.stdout
        self._save_err = sys.stderr
        sys.stdout = _Tee(self._save_out, self._out)
        sys.stderr = _Tee(self._save_err, self._err)

    def stopTest(self, test):
        sys.stdout = self._save_out
        sys.stderr = self._save_err
        unittest.TestResult.stopTest(self, test)

    def _record(self, test, outcome, message, tb):
        self.records.append(
            _case(
                _nodeid(self.prefix, test),
                outcome,
                (time.time() - self._t0) * 1000.0,
                "".join(self._out),
                "".join(self._err),
                message,
                tb,
            )
        )

    def _stop_on_maxfail(self):
        if self._maxfail and len(self.failures) + len(self.errors) >= self._maxfail:
            self.shouldStop = True

    def _record_err(self, test, outcome, err):
        self._record(test, outcome, str(err[1]), _exc_text(err))
        self._stop_on_maxfail()

    def addSuccess(self, test):
        unittest.TestResult.addSuccess(self, test)
        self._record(test, _PASSED, "", "")

    def addFailure(self, test, err):
        unittest.TestResult.addFailure(self, test, err)
        self._record_err(test, _FAILED, err)

    def addError(self, test, err):
        unittest.TestResult.addError(self, test, err)
        if type(test).__name__ == _HOLDER:
            self.collection_errors.append(
                _collect_err(_holder_nodeid(self.prefix, test), "", str(err[1]), _exc_text(err))
            )
            self._stop_on_maxfail()
            return
        self._record_err(test, _ERROR, err)

    def addExpectedFailure(self, test, err):
        unittest.TestResult.addExpectedFailure(self, test, err)
        self._record(test, _PASSED, str(err[1]), _exc_text(err))

    def addUnexpectedSuccess(self, test):
        unittest.TestResult.addUnexpectedSuccess(self, test)
        self._record(test, _FAILED, "unexpected success", "")

    def addSubTest(self, test, subtest, err):
        add_sub = getattr(unittest.TestResult, "addSubTest", None)
        if add_sub is not None:
            add_sub(self, test, subtest, err)
        if err is None:
            self._record(subtest, _PASSED, "", "")
        else:
            self._record_err(subtest, _FAILED, err)

    def addSkip(self, test, reason):
        unittest.TestResult.addSkip(self, test, reason)
        self._record(test, _SKIPPED, reason, "")


def _filter_suite(suite, prefix, selected):
    if not selected:
        return suite
    filtered = unittest.TestSuite()
    pending = [suite]
    while pending:
        item = pending.pop()
        if isinstance(item, unittest.TestSuite):
            pending.extend(list(item))
        elif _matches(_nodeid(prefix, item), selected):
            filtered.addTest(item)
    return filtered


def _push_import_roots(test_path, workspace_root):
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


def _pop_import_roots(inserted):
    for path in reversed(inserted):
        try:
            sys.path.remove(path)
        except ValueError:
            pass


def _run_file(request):
    test_path = request.get("test_path") or ""
    workspace_root = request.get("workspace_root") or ""
    prefix = request.get("nodeid_prefix") or test_path.replace("\\", "/")
    selected = request.get("selected") or []
    maxfail = int(request.get("maxfail") or 0)

    inserted = _push_import_roots(test_path, workspace_root)
    try:
        module = _load_source("_ipy_under_test", test_path)
        suite = _filter_suite(
            unittest.TestLoader().loadTestsFromModule(module),
            prefix,
            selected,
        )
        result = JsonTestResult(prefix, maxfail)
        suite.run(result)
        errors = list(result.collection_errors)
        if not result.records:
            errors.append(
                _collect_err(prefix, test_path, "No unittest.TestCase tests ran in this file.", "")
            )
        return _payload(result.records, errors)
    finally:
        _pop_import_roots(inserted)


def _run():
    driver_dir = os.path.dirname(os.path.abspath(__file__))
    request_path, default_result = _sidecar_paths(driver_dir)
    result_path = default_result
    try:
        with open(request_path, "r") as req:
            request = json.loads(req.read())
        if not isinstance(request, dict):
            raise TypeError("request must be a JSON object")
        result_path = request.get("result_path") or default_result
        payload = _run_file(request)
    except Exception:  # noqa: BLE001
        payload = _payload(
            [],
            [_collect_err("", request_path, "IronPython test driver failed.", traceback.format_exc())],
        )
    with open(result_path or default_result, "w") as out:
        out.write(json.dumps(payload))


_run()
