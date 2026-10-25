# Testing Overview

Automated tests run **inside a live host** so they can work with the active document and host API. RevitDevTool supports Python tests with pytest and .NET tests with Microsoft Testing Platform.

<LogoRow items={['pytest', 'nunit', 'tunit', 'mstest']} />

:::info Hosts
Tests run on [Revit](/docs/hosts/Hosts-Revit) and the [AutoCAD family](/docs/hosts/Hosts-AutoCAD), 2022–2027. Pick the product with the [host settings](#host-settings).
:::

:::tip Versions match the latest release
The package versions on these pages are the ones that work with the latest RevitDevTool.
:::

---

## Choose a Stack

| Stack | Framework | Package | Entry doc |
| --- | --- | --- | --- |
| **.NET** | NUnit `5.0.0` (default), TUnit `1.76.0`, or MSTest `4.5.1` | NuGet: [RevitDevTool.TestAdapter](https://www.nuget.org/packages/RevitDevTool.TestAdapter/0.1.4) `0.1.4` | [NUnit](/docs/testing/NUnit) · [TUnit](/docs/testing/TUnit) · [MSTest](/docs/testing/MSTest) |
| **CPython** | pytest | PyPI: [revitdevtool_pytest 0.5.0](https://pypi.org/project/revitdevtool_pytest/0.5.0/) | [pytest](/docs/testing/pytest) |
| **IronPython** | unittest (`TestCase`) | Name files `test_*_ipy.py`; pytest discovers them as IronPython tests | [unittest](/docs/testing/unittest) |

### IronPython notes

- Name files `test_*_ipy.py` so the test client recognizes them as IronPython tests.
- On **Revit**, engine preference matches IronPython execution: **pyRevit first** when installed, else IronPython 3.4.2. AutoCAD-family hosts use bundled IronPython 3.4.2.
- No pytest fixtures and no PEP 723. You can still debug IronPython tests: attach your IDE to the IronPython port before running ([Attach Debugger](/docs/execution/python/Execution-PythonDebugging)).
- Mixed `test_*.py` + `test_*_ipy.py` under one `conftest.py` is fine; separate `conftest.py` trees require separate pytest invocations against the same host.

---

## How tests run

<SequenceFlow
  participants={[
    { id: 'runner', title: 'Test runner', subtitle: 'pytest, dotnet test, IDE' },
    { id: 'host', title: 'Revit / AutoCAD', subtitle: 'RevitDevTool' },
  ]}
  steps={[
    { from: 'runner', to: 'host', text: 'Find the host, or launch one', detail: 'The runner connects to a running host, or starts a new one' },
    { from: 'runner', to: 'host', text: 'Run test', detail: 'Each test runs inside the host, with the real document and API' },
    { from: 'host', to: 'runner', text: 'Pass, fail, output', back: true, detail: 'The result of each test comes back; this repeats for every test' },
    { note: 'Results appear in your terminal or IDE', over: ['runner', 'host'], detail: 'You read the results where you started the run' },
  ]}
/>

- The runner starts or connects to the selected Autodesk application.
- Each test runs with the matching Python or .NET runtime, and the result comes back to your terminal or IDE.
- Tests that change a document should clean up after themselves and respect the host's transaction rules.

**For .NET tests**

- Add the [`RevitDevTool.TestAdapter`](https://www.nuget.org/packages/RevitDevTool.TestAdapter/0.1.4) package. It runs NUnit, TUnit and MSTest inside the host through Microsoft Testing Platform `2.5.1`.
- NUnit is the default. Set `TestingFramework` to `tunit` or `mstest` to opt in.
- Pin the framework package yourself; the adapter does not pull it.

---

## Host settings

Every test run needs to know which host to use. The settings are the same for .NET and Python; only where you set them differs.

| Setting | .NET (`.csproj`) | pytest (CLI / `pyproject.toml`) | Meaning | Default |
| --- | --- | --- | --- | --- |
| Host | `HostName` | `--host` / `host_name` | The product to test against. Required for .NET | `revit` for pytest |
| Version | `HostVersion` | `--host-version` / `host_version` | The Autodesk year. For .NET it must match the API references used to compile the tests | none; required for .NET and with force launch |
| Fresh host | `ForceLaunch` | `--force-launch` / `force_launch` | Start a new host instead of reusing a running one | `false` |
| Launch timeout | `LaunchTimeout` | `--launch-timeout` / `launch_timeout` | Seconds to wait for the host to start | `180` for .NET, `120` for pytest |
| Test timeout | `PerTestTimeout` | `--per-test-timeout` / `per_test_timeout` | Seconds allowed per test | `60` |
| Known host | not available | `--host-pipe` / `host_pipe` | Connect to a specific running host by pipe name | none |

Use these values to pick the product:

| Product | .NET `HostName` | pytest `host_name` |
| --- | --- | --- |
| Revit | `Revit` | `revit` |
| AutoCAD | `AutoCad` | `autocad` |
| Civil 3D | `Civil3D` | `civil3d` |
| Plant 3D | `Plant3D` | `plant3d` |
| Map 3D | `AcadMap3D` | `acadmap3d` |
| Architecture | `AcadArch` | `acadarch` |
| Mechanical | `AcadMech` | `acadmech` |
| MEP | `AcadMep` | `acadmep` |
| Electrical | `AcadElec` | `acadelec` |

:::tip Plain AutoCAD
Use `AutoCad` / `autocad` for plain AutoCAD, and the product-specific value when you test against a vertical.
:::

---

## Leaf Docs

| Doc | Covers |
| --- | --- |
| [pytest](/docs/testing/pytest) | CPython pytest setup, configuration, fixtures, PEP 723, `--host` table |
| [NUnit](/docs/testing/NUnit) · [TUnit](/docs/testing/TUnit) · [MSTest](/docs/testing/MSTest) | Microsoft Testing Platform project setup, pinned framework packages, CLI and debugger |

**Samples:** [RevitDevTool.PyTest](https://github.com/trgiangv/RevitDevTool.PyTest) (Python) · [DevTools.NUnit.SampleTests](https://github.com/trgiangv/RevitDevTool/tree/main/samples/DevTools.NUnit.SampleTests), [DevTools.TUnit.SampleTests](https://github.com/trgiangv/RevitDevTool/tree/main/samples/DevTools.TUnit.SampleTests), [DevTools.MSTest.SampleTests](https://github.com/trgiangv/RevitDevTool/tree/main/samples/DevTools.MSTest.SampleTests) (.NET)

**Troubleshooting:** [Troubleshooting](/docs/getting-started/Troubleshooting) · bridge connection issues · [Known Limitations](/docs/getting-started/Known-Limitations) (IronPython split, sequential runs)
