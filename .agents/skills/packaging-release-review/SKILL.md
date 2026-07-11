# Packaging Release Review

Use when editing build modules, installer creation, bundle layout, release publishing, or host packaging.

## Checklist

- Read `docs/agents/build-matrix.md` and `docs/agents/verification.md`.
- Treat `build/Program.cs` and `build/Modules/*` as the pipeline source of truth.
- Preserve `Release.Autodesk.*` selection from `RevitDevTool.slnx`.
- Remember Revit and AutoCAD currently publish into `RevitDevTool.bundle`.
- Do not re-enable ILRepack for 2027 host projects without validating Autodesk isolated context behavior.
- Update `docs/agents/build-matrix.md`, `docs/agents/verification.md`, or release/package docs when changing supported configurations, package layout, or release commands.
- Run `scripts/pack.ps1` for full package verification when practical.
