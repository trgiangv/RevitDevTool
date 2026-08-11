# NUnit Host Testing (Agent Digest)

Experimental. Product contract: `docs/product/nunit-host-testing.md`.
ADR: `docs/decisions/0015-nunit-host-testing-standard-integration.md`.

## Verify

```powershell
# Shared DevTools.* — no Deploy*/IsRepackable props (UseRevit/UseAutoCad not set)
dotnet build source/DevTools.NUnit.Host/DevTools.NUnit.Host.csproj -c Debug
scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Host.Tests/DevTools.NUnit.Host.Tests.csproj
scripts/test-dotnet.ps1 -Project tests/DevTools.NUnit.Core.Tests/DevTools.NUnit.Core.Tests.csproj
# Live smoke (host running):
# DevTools.NUnit.Runner run <SampleTests.dll> --host Revit --version 2024
```

Deploy **Revit/Acad host** after Host DLL changes land in the add-in: `scripts/build-host.ps1 -Year <year>`.
Publish Runner to bundle: `dotnet publish source/DevTools.NUnit.Runner -c Release`.

Build flag rules: `.agents/skills/build/SKILL.md` (deploy props only for `UseRevit` / `UseAutoCad` projects).

## Traps

- Do not use `NUnit.Engine` in the host (Dynamo `nunit.framework` clash).
- Probe dirs use `DirectoryAssemblyLoad` (shadow); deploy folder uses `AssemblyLoader`.
- Adapter discovers locally; run goes through Runner → pipe — not testhost execution.
- `--debug` / Test Explorer Debug is **not supported** (experimental deferral).
- NuGet `DevTools.NUnit.TestAdapter` is **not published** yet.
- Test projects must ship matching `nunit.framework` beside the test DLL.

## Gaps

See product doc “Gaps” — debugging, MTP, full attributes, public NuGet, broad CI.
