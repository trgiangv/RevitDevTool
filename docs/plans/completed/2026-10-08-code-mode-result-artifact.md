# Execution Plan: saved `code_mode` results

Date: 2026-10-08

## Status

Completed. [0041](../../decisions/0041-code-mode-result-artifact.md) is Accepted.

## Outcome

A `code_mode` text return over 40_000 characters is written to one flat
`%TEMP%\RevitDevTool-{pid}-{id}.txt`. The model receives the start, the end,
and that path. Structured JSON on a returned `CallToolResult` counts as that
text. A return at or under 40_000 characters is unchanged. Image, audio, and
blob payloads still use the 1 MiB budget and are not saved.

## Context

Decision: [0041](../../decisions/0041-code-mode-result-artifact.md).

Host query tools return every match. The Daemon is the place that keeps a
large return from filling the model context.

## Scope

In scope:

- `CodeModeResultStore.Save` and `CodeModeResult.PreviewResult`.
- Structured JSON on a returned `CallToolResult` included in the measured text.
- 64 MB file cap and 256 MB cap for files this process owns.
- Description on `code_mode`, the 0040 quote, and `docs/product/mcp.md`.
- Focused tests in `DevTools.Daemon.Tests`.
- `revit_find_elements` and `acad_find_entities` return every match.

Out of scope:

- A 16 KB gate, or a 1 MiB drop for text.
- JSONL, a summary, a sample, or `ReadSavedResult`.
- A hosted service that scans `%TEMP%`.
- Saving image, audio, or blob bytes.
- The schedule preview row cap.

## Progress

- [x] 0041 Accepted.
- [x] Save, preview, cap, and structured-JSON tests.
- [x] Description, 0040 quote, and product paragraph.
- [x] Query tools no longer page results.
- [x] Live `code_mode` return of `revit_find_elements` on a restarted Daemon.

## Validation

- `dotnet run --project tests/DevTools.Daemon.Tests/DevTools.Daemon.Tests.csproj -- --filter "CodeModeResultTests|CodeModeResultStoreTests"` — 13 passed.
- `dotnet build samples/RevitMcpToolSet/RevitMcpToolSet.csproj -c Debug.Autodesk.2025 -p:DeployRevitAddin=false -p:DeployAutoCadBundle=false -p:ILRepackable=false` — succeeded.

## Result

Live on Revit 2025 process 45828: `revit_find_elements` returned 9664 elements.
The Daemon joined the text summary and the structured JSON into 1_994_873
characters, returned the start, the end, and
`%TEMP%\RevitDevTool-29316-a2e35e22138ce820.txt`. That file is the full text.
