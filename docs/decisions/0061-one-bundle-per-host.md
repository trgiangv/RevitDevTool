# 0061 One Bundle per Host

Date: 2026-11-10

## Status

**Accepted 2026-11-10.** Replaces the payload unit and the release assets of
[0046](0046-per-year-release-payloads-and-update-manifest.md). The manifest model,
signatures, and draft-then-publish of 0046 stay.

## Context

0046 shipped one `RevitDevTool.bundle` whose `Contents\<host version>` held both
hosts. A Revit-only user downloaded the AutoCAD files too. Measured on 2025:

| Part | On disk | Zip |
|------|---------|-----|
| Files every host publishes | 35 MB | 12.6 MB |
| `RevitDevTool.*` | 49.5 MB | 20.4 MB |
| `AcadDevTool.*` | 48 MB | 20.0 MB |

0046 rejected per-host payloads because two zips would write into one folder,
and the folder is the unit of download, apply, and lock (0049).

## Decision

- Each host has its own bundle in `%APPDATA%\Autodesk\ApplicationPlugins`:
  `RevitDevTool.bundle` (Revit components) and `AcadDevTool.bundle` (AutoCAD
  components). Each `Contents\<host version>` holds the shared files and that
  host's files. Installing both hosts keeps the shared files twice.
- The payload unit is the **add-in target** (platform plus host version): one
  folder, one zip, one lock. 0049 applies unchanged per folder.
- The templates are `props/<bundle>/PackageContents.xml`. A platform belongs to
  the bundle whose template lists it.
- `installation.json` moves to `%APPDATA%\RevitDevTool\`, because it covers both
  bundles. Each bundle has its own `.updates` and rollback.
- `update-manifest.json` is schema 2. A payload names one `platform` and one
  `hostVersion`, not a `platforms` list.
- Release assets: `RevitDevTool-<v>-<host version>.zip`,
  `AcadDevTool-<v>-<host version>.zip`, `RevitDevTool.bundle.zip`,
  `AcadDevTool.bundle.zip`. `--source` takes one offline zip per bundle.
- The compile keeps one folder per host version, `output/hosts/<v>`, with
  `shared` (files every host publishes, byte-identical) and one folder per
  bundle. `pack addin` builds each bundle from `shared` plus its own folder.

## Alternatives Considered

1. **One bundle, three zips per host version (shared, Revit, AutoCAD).** No
   duplicate on disk, but three zips write into one folder: the unit problem
   0046 rejected.
2. **Keep 0046.** Revit-only and AutoCAD-only users download about 60% more.

## Consequences

Positive:

- A single-host user downloads about 33 MB per host version instead of 53 MB.
- A busy Revit no longer holds the AutoCAD update of the same host version.

Tradeoffs:

- A user with both hosts downloads about 13 MB more per host version and keeps
  the shared files twice.
- Twice the payload assets per release.
- The manifest schema changed; there is no reader for schema 1.

## Follow-Up

- None.
