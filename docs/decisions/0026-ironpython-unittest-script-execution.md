# 0026 One IronPython Unittest Flow, Dialect 2.7 And 3.4

Date: 2026-08-30

## Status

Proposed

Amended after review: a **single** `_ipy` flow. Engine selection matches
script execution — pyRevit first, embedded IronPython 3.4.2 otherwise.
The pipeline (collect, wire, in-engine runner, test source) must run on
**IronPython 2.7 and 3.4.2**. Does not change runtime behavior until
accepted and implemented.

## Context

RevitDevTool runs Python on two unrelated runtimes.

`ExecutionMode.Python` uses Python.NET / CPython. In-host tests go through
the pytest bridge (`tests/run`, `RevitDevTool.PyTest`). pytest 9 cannot run
on IronPython.

`ExecutionMode.IronPython` (`*_ipy_script.py`) already has **one** execution
flow: `RevitIPyExecutionStrategy` uses pyRevit Labs/`PyRevitLoader` when
loaded, else embedded IronPython 3.4.2. pyRevit clones may be IronPython
2.7 or 3.x. That auto-pick is the product IronPython runtime, not a second
test product.

`unittest.main()` already executes on the embedded path (`ModuleName =
"__main__"`), but `SystemExitException` is reported as success.
pyRevit `IsSuccessResultCode` treats `Succeeded = 0` and `SysExited = 1` as
success. A failing suite currently reports success on both backends.

`unittest.TestCase` is not a runtime signal: the same form already runs on
CPython via pytest. A file named `test_*_ipy_script.py` is collected by
pytest `test_*.py` and executed on Python.NET.

Review judgments: do not pin `embedded` vs `pyrevit` as two test protocols;
keep one `_ipy` flow; design the pipeline for 2.7 and 3.4.2 because the
engine behind that flow is whichever IronPython execution would use.

## Decision

1. **One IronPython flow, same engine policy as scripts.** Tests reuse
   `RevitIPyExecutionStrategy` (and AutoCAD's embedded IPy where pyRevit
   does not exist). No `engine=` request field, no `*_pyrevit_test.py`,
   no test-only override of pyRevit-first. Which engine ran may be echoed
   in diagnostics; it is not a selector.

2. **Pytest collect may use `test_*_ipy.py`; the host does not.**
   Local pytest needs a fail-closed way to split CPython `tests/run` from
   IronPython `ipytests/run` without importing the module. Filename
   `test_*_ipy.py` is that **client** convention (pytest `test_` plus an
   `_ipy` marker). It is not a runtime identity: `ipytests/run` runs
   unittest on the requested paths. `unittest.TestCase` is the in-engine
   runner, not the classifier.

   | Pattern (pytest collect) | Runtime | Protocol |
   |---------|---------|----------|
   | `test_*.py` excluding `test_*_ipy.py` | CPython.NET | pytest `tests/run` |
   | `test_*_ipy.py` | IronPython | unittest `ipytests/run` |
   | `*_ipy_script.py` | execution-tree script | not a test file |

   A `test_*_ipy.py` file that is not `unittest.TestCase` is a collection
   error on the client. Ordinary `test_*.py` stays on CPython pytest.
   Production / library modules must not use `test_*_ipy.py` — that name
   exists only so pytest can route.

3. **The IPy pipeline is a 2.7 / 3.4.2 intersection.** Anything executed
   inside the IronPython engine — user tests **and** any hosted runner
   snippet — must parse and run on both. Forbidden in that dialect: f-strings,
   `async`/`await`, type annotations that 2.7 cannot parse, the `print x`
   statement (use `print(x)` — valid on 2.7 and 3.4 without `__future__`), pytest
   fixtures, PEP 723, CPython-only stdlib. Prefer driving unittest from C#
   (DLR / pyRevit script path) so the runner itself is not a Python 3 file.

4. **`RevitDevTool.PyTest` may expand as collect/report shell only.**
   Intercept `test_*_ipy.py` (`pytest_collect_file`), build nodeids **without
   importing the module in CPython**, dispatch on `ipytests/run`. Do not
   send those files through `tests/run` / `pytest.main()`.

5. **Local collect must not assume Python 3.** Do not `import` IPy test
   files. Do not rely on `ast.parse` (Python 3). Use filename plus a 2/3-safe
   scan (`tokenize` / line patterns) for `unittest.TestCase` and `test_*`.
   If per-test names cannot be recovered, one nodeid = the file (whole-file
   suite). That is the compatible default, not a failure.

6. **`tests/run` stays CPython + Python.NET.** Do not overload
   `PytestRunner.py`. Python testing never enters the MTP kernel.

7. **`*_ipy_script.py` remains script execution.** A `__main__` unittest
   self-check is allowed. `SystemExit` / pyRevit `SysExited` must yield a
   truthful `ExecutionResult`.

## Alternatives Considered

1. **Two pinned engines (`test_*_ipy.py` vs `*_pyrevit_test.py`).** Rejected
   after review. Duplicates a distinction the execution tree already
   resolves, and authors would maintain two suites for one product flow.

2. **IronPython stays scripts-only.** Rejected. Automated collect/report
   through the existing pytest client is still wanted; engine count is what
   collapsed, not the client shell.

3. **Identify IPy tests by `unittest.TestCase` or a pytest marker.** Rejected
   as primary identity. Filename `_ipy` is fail-closed and matches
   `_ipy_script.py`.

4. **Route `test_*_ipy.py` through `tests/run`.** Rejected. Host
   `pytest.main()` cannot run on IronPython 2.7 or 3.4.

5. **Fold into MTP.** Rejected ([0021](0021-testing-kernel-and-provider-owned-framework-runtime.md),
   [0024](0024-testing-core-open-closed-providers.md)).

6. **Author tests in Python 3.4-only and skip 2.7.** Rejected. pyRevit-first
   means 2.7 is in the production path on many machines.

## Consequences

Positive:

- one `_ipy` contract for scripts and tests;
- pytest IDE tree can list IPy tests without executing them on Python.NET;
- CPython `PytestContracts` stay a single mirror;
- tests exercise the same engine users get when they run `*_ipy_script.py`.

Tradeoffs:

- results differ across machines (pyRevit present vs only 3.4.2); that is
  accepted as matching script execution, and should be visible in the run
  diagnostic;
- test source is stuck on the 2.7/3.4 intersection — no f-strings, no
  pytest fixtures;
- per-test nodeids may be unavailable when the file is 2.7-only; whole-file
  suites are first-class;
- host needs an IronPython unittest runner plus `ipytests/run`;
- truthful `SystemExit` mapping remains a defect fix on both backends.

## Follow-Up

- Spike: `SystemExitException` on 3.4.2 and pyRevit `SysExited` vs a failed
  unittest exit code — both backends, one flow.
- Spike: selected test ids vs whole-file on the pyRevit script-path API;
  whole-file is the compatible v1 if selection cannot be 2.7-safe.
- Plugin: collect `test_*_ipy.py` without CPython import; 2/3-safe scan.
- IPy scripts stay `*_ipy_script.py` (no `test_` prefix) so pytest does not collect them.
- After accept: product layer `docs/product/execution.md` — one `_ipy`
  authoring dialect. One layer only.
