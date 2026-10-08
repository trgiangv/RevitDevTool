# 0041 Large `code_mode` results are saved

Date: 2026-10-08

## Status

**Accepted 2026-10-08.** There is no 16 KB gate. Text is not dropped at
1 MiB. Image, audio, and blob payloads still use the 1 MiB budget from
[0040](0040-bm25f-search-and-hit-annotations.md).

## Context

`code_mode` runs a C# body in the Daemon. The model is supposed to receive
a small projection. A host query can still return a full element table as
one string. Under 1 MiB the Daemon sends that string whole. Over 1 MiB the
Daemon drops it. MCP does not require a client to keep a second copy.

Pi, releases 0.99.2 through 1.1.0, does keep a second copy, and only on the
machine running Pi. `output-files.ts` writes every such file to the OS temp
directory, mode `0600`, with a random name and `wx` so it will not follow a
planted link. The model is told to open that path with Pi's `read` tool.
Three paths, three budgets:

| What the model called | View the model sees | Full copy |
|---|---|---|
| `bash`, and the display of `read` / `grep` / `find` / `ls` | Last 50 KB or 2_000 lines for bash. `OutputAccumulator` streams the bytes to disk once the view cap is passed. | `full_output_path` on the tool result. A codemode script that calls `bash` receives up to 1 MiB, then head and tail of 512 KiB, with the same path. |
| An MCP tool, directly | `limitMcpContent` keeps 20 KB, start and end, Codex-style. | `pi-mcp-*.txt`. The script value is separate: Pi wraps every MCP tool in an output schema, and `toScriptValue` returns `structuredContent`, which is the original `CallToolResult`, not the 20 KB text. |
| A codemode script's own output | `max_output_tokens` 10_000, at 4 characters per token. Start and end. | `pi-codemode-*.txt`. The Snowdon dump (about 20_963 tokens, one line) was this file. Head and tail of one JSON line are not records, and a line offset does not split that line. |

A script dies at 16 Mi characters or 100_000 `text()`, `image()`, and
`console` calls. `store()` is a different mechanism: small JSON kept on the
session branch, 256 KiB per value and 1 MiB total, for ids and cursors.
`image()` uses the same temp-file helper so a later turn can move the bytes.
The `codemode` description stays short and does not list deferred MCP tools.
0040 already follows that shape.

The half worth copying is the split: a bounded view, a complete retained
copy, and a later read that does not put the copy back into the prompt.
The file path does not travel. It exists only if that client writes it,
the model has a file reader, and the reader runs on that same machine.
A client that only renders `CallToolResult` has none of those. A cloud
model cannot open `%TEMP%\pi-mcp-*.txt`.

The Daemon covers that split for every client. The model is not the
component that reduces a dump, and the client is not the component that
keeps it. Counting, schema, and retention are mechanical. The model keeps
the questions that are actually uncertain: which slice matters, whether
the live model has moved, what an outlier means.

## Decision

### 1. The Daemon keeps the full text and returns the start, the end, and the path

There is no 1 MiB drop for text, and no 16 KB gate. When the text of a
`code_mode` return exceeds 40_000 characters, the Daemon writes that text
to a file and returns the start, the end, and the path. The file holds the
full text. Structured JSON on a returned `CallToolResult` counts as that
text.

40_000 characters is Pi's codemode view: `max_output_tokens` 10_000 at 4
characters per token. The return keeps 20_000 characters from the start and
20_000 from the end, then one line `[Full output: path]`. A return at or
under 40_000 characters is sent as it is.

A return that is only image, audio, or blob still uses the 1 MiB budget
from [0040](0040-bm25f-search-and-hit-annotations.md) and is not saved.

The file is `%TEMP%\RevitDevTool-{pid}-{id}.txt`, flat in the temp
directory. One save is one file. The pid keeps two Daemons apart. The file
is created with create-new so an existing name is not overwritten. After the
process exits, Windows removes the unused file: SilentCleanup (`cleanmgr.exe`)
runs daily with Automatic Maintenance and deletes `%TEMP%` entries whose last
access is older than 7 days, and Storage Sense deletes temporary files that
are not in use. The Daemon does not scan the temp directory.

One saved file is capped at 64 MB. The files this process owns are
capped at 256 MB. A save that would pass either cap returns an error and
does not leave that file.

### 2. The model reads the path when the start and end are not enough

The saved call still has no `outputSchema` and does not set
`StructuredContent`. The text block is the start, the end, and the path.
Image and audio blocks on the same return stay with that text. A host tool
answers a question about the live model. The file answers a question about
this return.

## Alternatives Considered

1. Keep the 1 MiB error and drop the payload. Rejected. The rows already
   paid for are lost, and a client that never received them cannot save them.
2. Send any text under 1 MiB and let the client truncate. Rejected. That
   is the gate this decision removes. Pi would cut it; another client may
   cut it with no retained copy.
3. Save at 16 KB, or return a truncated prefix. Rejected. 16 KB is not
   Pi's view. A prefix is not a summary.
4. Copy Pi `store()` for the element table. Rejected. The cap is 256 KiB
   per value and 1 MiB total, and Pi reserves it for small state.
5. Gzip or a second summarizer model. Rejected. The model cannot read
   compressed bytes, and counts must come from code.
6. Let the host script pick a path and call `File` itself. Rejected. The
   Daemon owns the file and the path.
7. Hide the path and return only a summary. Rejected. Pi returns the start,
   the end, and the path of the full text. The model uses that path when
   the view is not enough.

## Consequences

Positive:

- Every client gets a retained copy once the text passes the view, not only
  Pi.
- The model sees the start, the end, and the path of the full text.
- A short return, at or under 40_000 characters, is sent as it is.
- Revit is not held open while the model decides the next question.

Tradeoffs:

- The file stays until Windows temp cleanup removes it. A later Daemon does
  not revive that return.
- The host query still materializes its result before `code_mode` returns.
  Saving it keeps the model context small. It does not reduce Revit memory during the query.
- A `CallToolResult` whose text exceeds 40_000 characters saves that text.
  Image and audio blocks on that same result are not copied into the file.
- Image, audio, and blob results still drop at 1 MiB. That budget is not
  this split.
- `code_mode`'s description and [0040](0040-bm25f-search-and-hit-annotations.md)'s
  quoted description must mention the path when this
  lands. Product behavior is updated in `docs/product/mcp.md` in that same
  change.

## Follow-Up

- Implementation plan: [2026-10-08-code-mode-result-artifact](../plans/completed/2026-10-08-code-mode-result-artifact.md).
