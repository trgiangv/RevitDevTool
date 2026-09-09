# Testing Overview

Automated tests run **inside a live host** so they can work with the active document and host API. RevitDevTool supports Python tests with pytest and .NET tests with Microsoft Testing Platform.

> **Claimed hosts:** [Revit](/docs/hosts/Hosts-Revit) and [AutoCAD-family](/docs/hosts/Hosts-AutoCAD) (2022–2027). Vertical-specific `--host` / `HostName` values exist for routing; they are not separate product lines.

---

## Choose a Stack

| Stack | Framework | Package | Entry doc |
| --- | --- | --- | --- |
| **.NET** | NUnit `4.6.1` or TUnit `1.66.27` | NuGet: [RevitDevTool.TestAdapter 0.0.7](https://www.nuget.org/packages/RevitDevTool.TestAdapter/0.0.7) | [NUnit](/docs/testing/NUnit) · [TUnit](/docs/testing/TUnit) |
| **CPython** | pytest | PyPI: [revitdevtool_pytest 0.4.0](https://pypi.org/project/revitdevtool_pytest/0.4.0/) | [pytest](/docs/testing/pytest) |
| **IronPython** | unittest (`TestCase`) | Name files `test_*_ipy.py`; pytest discovers them as IronPython tests | [unittest](/docs/testing/unittest) |

### IronPython notes

- Name files `test_*_ipy.py` so the test client recognizes them as IronPython tests.
- On **Revit**, engine preference matches IronPython execution: **pyRevit first** when installed (default **IronPython 2.7.12**), else IronPython 3.4.2. AutoCAD-family hosts use bundled IronPython 3.4.2.
- No pytest fixtures, no PEP 723, **no debugger** for IronPython tests.
- Mixed `test_*.py` + `test_*_ipy.py` under one `conftest.py` is fine; separate `conftest.py` trees require separate pytest invocations against the same host.

---

## How tests run

The test runner starts or connects to the selected Autodesk application, runs each test with the appropriate Python or .NET runtime, and reports the result back to your terminal or IDE. Tests that modify a document should clean up after themselves and respect the host's transaction rules.

For .NET tests, `DevTools.TestAdapter` is the MTP adapter assembly. Add the public `RevitDevTool.TestAdapter` package to the test project; it includes the host integration for both NUnit and TUnit. The repository currently aligns Microsoft Testing Platform packages to `2.4.0`.

---

## Leaf Docs

| Doc | Covers |
| --- | --- |
| [pytest](/docs/testing/pytest) | CPython pytest setup, configuration, fixtures, PEP 723, `--host` table |
| [NUnit](/docs/testing/NUnit) · [TUnit](/docs/testing/TUnit) | Microsoft Testing Platform project setup, pinned framework packages, CLI and debugger |

**Samples:** [RevitDevTool.PyTest](https://github.com/trgiangv/RevitDevTool.PyTest) (Python) · [DevTools.NUnit.SampleTests](https://github.com/trgiangv/RevitDevTool/tree/main/samples/DevTools.NUnit.SampleTests), [DevTools.TUnit.SampleTests](https://github.com/trgiangv/RevitDevTool/tree/main/samples/DevTools.TUnit.SampleTests) (.NET)

**Troubleshooting:** [Troubleshooting](/docs/getting-started/Troubleshooting) · bridge connection issues · [Known Limitations](/docs/getting-started/Known-Limitations) (IronPython split, sequential runs)
