# Injects compact harness commands so agents skip build-skill roundtrips.
. "$PSScriptRoot\lib.ps1"

$context = @'
## RevitDevTool harness (session)

Compile verification runs automatically via Cursor stop hook after .cs/.csproj/.xaml edits. Do not read a build skill or invent MSBuild commands for routine verify.

### What the hook builds
- Revit API projects (`UseRevit`): Debug.Autodesk.2022 + 2025 + 2027, compile-only (no deploy/ILRepack).
- AutoCAD API projects (`UseAutoCad`): same year matrix, compile-only.
- Shared `DevTools.*` / multi-TFM: `Debug` (all TargetFrameworks). Prefer Release only when packaging.

### Manual commands (only when needed)
- Deploy host: `scripts/kill-host.ps1` then `scripts/build-host.ps1 -Year 2025`
- .NET tests: `scripts/test-dotnet.ps1`
- PyTest client (RevitDevTool.PyTest repo): `uv run pytest -v` (never search system Python / bare pytest).
- Package: `scripts/pack.ps1`

For domain workflows, read the matching skill under `.agents/skills/` when the task fits. Do not invent skills.
'@

Write-HookJson @{ additional_context = $context }
exit 0
