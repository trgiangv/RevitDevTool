# 0040 BM25F inside `code_mode`

Date: 2026-10-07

## Status

**Accepted 2026-10-07.** Supersedes the scoring bullet in
[0039](0039-mcp-flow-audit-sdk-reuse-and-vocabulary.md) decision 9 and
alternative 5 ("no BM25"). Amends
[0027](0027-mcp-product-surface.md): the model-facing dynamic tool is
`code_mode` with a C# body. `search_dynamic` and `invoke_dynamic` leave
`tools/list` when the surface lane of the active plan lands.
Host capabilities still never appear on `tools/list`. Clients still never
call host tool names or open the host pipe.

## Context

A normal MCP agent and a Pi Code Mode session can share the same Daemon and
the same host pipe. The session that finishes in two model turns is the one
whose program searches, calls host tools, filters the payload, and returns a
small result. The session that takes many turns sends every intermediate
payload back to the model. Measured host and pipe time does not explain that
gap.

0027 names Cursor as the product client and defines the agent loop as
search → `id` → invoke → result, one MCP call per step. That loop is what
makes Cursor slow here. Pi is fast because the program runs outside the
model, not because the pipe is different. Pi does not expose `searchTools`
as a model-facing tool the model must call first. The script calls it.
Listing `search_dynamic` beside a program tool recreates the slow loop,
because the model will use the tool it can see.

The fixed tool list is still required: listing every host capability would
put hundreds of schemas in context. The pair is not. One tool,
`code_mode`, takes a C# program. Search stays in that program.

Pi's relevant surface, from the Code Mode docs
([pi.dev/docs/latest/codemode](https://pi.dev/docs/latest/codemode)):

| Pi | What it does | RevitDevTool today |
|----|----------------|--------------------|
| `searchTools(query)` | BM25 over tools the session can call. Default limit 8. Returns `{ name, description }` only. | `search_dynamic` is the catalog the model does not hold. Rank is a weighted token sum with a half-match cutoff. Default limit 12. |
| `describeTool(name)` | Description plus a TypeScript declaration, fetched when the script needs the shape. | `detail=schema` returns `inputSchema`. Summary omits it. |
| Tool annotations | Sit on the tool the session registered. The script calls that tool by name. | Host tools are not registered on the external server. The only callable write/read tool is `code_mode`, declared `Destructive=true`, `OpenWorld=true`. |
| Script body | Many calls, `Promise.all`, filter, then one result to the model. JavaScript, because Pi's runner is QuickJS. | No Daemon program tool. Host `execute_csharp` is a different tool: Roslyn inside the CAD process, Revit/AutoCAD API, not catalog orchestration. |

Two consequences follow from that table.

Search quality is a model-turn problem. `SearchIndex` already runs in memory
and does not open a pipe (`SearchTool` → `ProcessCatalogs` → `SearchIndex`).
The current rank drops a hit when fewer than half the query tokens appear
(`matched * 2 < queryTokens.Count` in `Scoring`). A query such as
"equipment information" against a tool whose text only contains "equipment"
scores 0 and disappears. The model searches again. 0039 alternative 5 rejected
BM25 because the catalog is small and the weighted sum was considered enough
recall without a dependency. The catalog is still small. The cost that matters
is the extra inference, not the scorer's CPU.

Risk metadata has nowhere to ride. `CatalogItem.Tool` is an SDK `Tool` and
already carries `Tool.Annotations` (`readOnlyHint`, `destructiveHint`,
`idempotentHint`, `openWorldHint`, `title`) from the .NET and Python parsers.
`SearchItem` does not copy them. A client that can inspect a Pi-style program
cannot tell a read from a write until it either calls `code_mode` or
treats every invoke as destructive. MCP annotations are properties of a tool
definition, not of one call's arguments, so changing them per `id` on
`code_mode` is not available.

## Decision

### 1. Rank with in-process BM25F

Replace `Scoring`'s weighted sum and half-match cutoff with Okapi BM25F.
No search library. No stemmer. No synonym or alias table. Tokenizer stays
`Tokenizer`: `_`, `-`, camelCase splits, then lowercase.

One document per `CatalogItem`. Four fields, weights chosen to keep the
0039 priority (target above name above description). Parameter names are
already posted in `SearchIndex.ToolSchemaPropertyNameTokens` and today do not
affect `Scoring`; they become a real field.

| Field | Weight | Source |
|-------|--------|--------|
| target | 4 | `CatalogItem.Target` |
| name | 2 | resource or template name; empty for a tool whose name is the target |
| description | 1 | `CatalogItem.Description` |
| parameters | 1 | input-schema property names; empty for resources and templates |

Constants: `k1 = 1.2`, `b = 0.75`.

For query token `t`, field `f`, item `d`, with `tf` the token count, `len_f`
the field's token count, `avg_f` the mean `len_f` over the indexed items
(0 treated as "skip the length norm", divisor = 1):

```text
weighted_tf(t,d) = Σ_f  weight_f * tf_f(t,d) / (1 - b + b * len_f(d) / avg_f)

IDF(t) = ln(1 + (N - df(t) + 0.5) / (df(t) + 0.5))

score(d) = Σ_t  IDF(t) * weighted_tf(t,d) * (k1 + 1) / (weighted_tf(t,d) + k1)
```

`N` is the number of indexed items. `df(t)` is the number of items that
contain `t` in any field. Both, plus `avg_f`, are fixed at index rebuild
(one process catalog change), not recomputed per query.

Query rules:

- Each distinct query token contributes once. Repeating a token in the query
  does not multiply the score.
- A token absent from the index adds nothing.
- An item whose `weighted_tf` is 0 for every query token is omitted.
- There is no half-match cutoff. A hit that matches one content token and
  misses filler tokens stays in the ranking.
- Empty or whitespace query returns no hits, as today.
- Order: score descending, then kind, target, processId. `limit` (default 12,
  max 32) and `hasMore` stay.
- `Match.Score` stays internal. The wire `SearchItem` does not gain a score.

Worked contrast, so the cutoff change is reviewable. Tool target
`GetMechanicalEquipment`, description "mechanical equipment". Query tokens
`equipment`, `information`.

- Today: one of two tokens match, `1 * 2 < 2` is false, so the hit is kept
  only because the cutoff is "fewer than half". Add a third unmatched token
  (`hvac`) and `1 * 2 < 3` drops the hit.
- BM25F: `equipment` contributes IDF and field tf. `information` and `hvac`
  contribute 0. The hit remains, ranked by `equipment` alone.

Second contrast. Many tools contain `get`; one tool's target contains
`equipment`. Query `get equipment`.

- Today: both tokens score +4 on any target that contains them, then the sum
  is divided by 2. `get` and `equipment` weigh the same.
- BM25F: `get` has a high `df` and a small IDF. `equipment` outranks it.

Known remainder: BM25F does not map `AHU` to "air handling unit", and it does
not stem `equipments` to `equipment`. Those misses can still cost a model
turn. They are out of scope here.

### 2. Copy tool annotations onto every search hit

Add an optional `annotations` object on `SearchItem`, for `kind=tool` only.
Copy it from `CatalogItem.Tool.Annotations`. Serialize with the Daemon source
generator (`McpServerJsonContext`, camelCase), same names the SDK already
emits for `tools/list`:

```json
"annotations": {
  "title": "Mechanical equipment",
  "readOnlyHint": true,
  "destructiveHint": false,
  "idempotentHint": true,
  "openWorldHint": false
}
```

Rules:

- Include a property only when the host set it. Do not invent `false`.
- Omit `annotations` when the tool has no annotation object, or when every
  hint is unset and `title` is empty.
- Put hints on `DescribeAsync`, not on `SearchAsync`. Pi's `searchTools` returns
  name and description. `describeTool` returns the declaration. A program
  that needs read versus write calls `DescribeAsync` in the same run.
- Resources and resource templates have no tool annotations. `ReadAsync`
  does not grow hints.

`code_mode` is `Destructive=true` and `OpenWorld=true` on `tools/list`.
Both hints describe the tool, not one call. A client approves the tool
before it can read `code` or `readOnly`.

`Destructive=true` because `InvokeAsync` can run a host tool that changes the
model. The worst call is a write, so the hint is a write. `readOnly: true`
is a runtime gate inside the Daemon. It does not flip this hint.

`OpenWorld=true` because the answer depends on a live host process. The
same `code` can return different elements after someone edits the model.
The hint does not mean network access or a third-party package. The
compile has neither.

The model does not see these fields on an MCP search result. `DescribeAsync`
returns them. The program branches on `ReadOnlyHint` before `InvokeAsync`.
The outer tool stays one destructive `code_mode`.

### Vocabulary

Identifiers are C#. Pi's JavaScript names are not copied onto the methods.
Methods that return `Task` take the `Async` suffix. Content types are the
MCP SDK types. The tool name on `tools/list` is not a C# identifier.

The model sees one tool, `code_mode`, with one field `code`. `run_csharp` is
not that name. It would sit beside host `execute_csharp` and tell the model
to write Revit API. `execute_csharp` stays a host capability found with
`SearchAsync` and called with `InvokeAsync`.

`SearchAsync`, `DescribeAsync`, `InvokeAsync`, and `ReadAsync` are methods
inside the program. They are not rows on `tools/list`.

| Role | Name |
|------|------|
| MCP tool | `code_mode` |
| JSON field for the body | `code` |
| Base class | `CatalogScript` |
| Entry point | `RunAsync` |
| Search the catalog | `SearchAsync` |
| Tool declaration | `DescribeAsync` |
| Call a host tool | `InvokeAsync` |
| Read a resource | `ReadAsync` |
| Host tool payload | SDK `CallToolResult` |
| Text, image, audio | SDK `TextContentBlock`, `ImageContentBlock`, `AudioContentBlock` |
| Embedded resource, link | SDK `EmbeddedResourceBlock`, `ResourceLinkBlock` |
| Resource read | SDK `ReadResourceResult` |
| Tool declaration | SDK `Tool` (`InputSchema`, `Annotations`) |

The body calls these methods with no receiver. There is no `api` object.
Result types are the MCP SDK types in `ModelContextProtocol.Protocol`,
the same objects `CallToolPassthroughAsync` already returns. The compile
references that assembly. It is already loaded in the Daemon. It is not a
new package and not a resolver. The script assembly is collectible and
references that non-collectible assembly. No second `ToolCallResult` or
`ResourceContents` type is declared.
The model does not see `id` or `target`. `Name` is the catalog target.
The daemon resolves `(processId, primitiveType, name)` to the current id.
`PrimitiveType` is `tool`, `resource`, or `resource_template`. It is the
MCP primitive of that item. The word `kind` is not used on this surface.
Prompt is also an SDK primitive and is not a value here.

`processId` stays the word from
[0039](0039-mcp-flow-audit-sdk-reuse-and-vocabulary.md). It is not renamed
to Pi's `namespace`.

Not methods in the body: `searchTools`, `describeTool`, `tools.<name>`,
`text`, `image`, `console`, `exit`, `store`, `load`, `ALL_TOOLS`,
`models`, `describeNamespace`, `tool_search`.
`list_processes` is how a caller learns process ids. The name matches
`ListProcessesTool`. It was `list_host_instances`.

`SearchAsync` defaults to tools only, limit 8. `processId` omitted means
every connected process. `DescribeAsync` and `InvokeAsync` require
`processId` when more than one process has that name. One match may omit it.

### 3. `code_mode` is the C# program

The model-facing tool is `code_mode`, snake_case like `list_processes` and
`launch_host`. Pi registers `codemode`
(`CODEMODE_TOOL_NAME` in `packages/coding-agent/src/extensions/codemode/tool.ts`).
Pi's field is JavaScript. Ours is a C# method body. The description says
so. `code_mode` does not accept `id`.

`invoke_dynamic` leaves `tools/list` with `search_dynamic`. It is the old
id contract (`id`, `arguments`, `reads`).

The Daemon compiles and runs the body. The tool result is the return value.
Search hits and host payloads stay in the Daemon unless the program returns
them.

Keep the index, the BM25F ranker, and `CallToolPassthroughAsync`. They sit
behind `SearchAsync`, `DescribeAsync`, `InvokeAsync`, and `ReadAsync`. A
one-call program is `return await InvokeAsync(...)`. There is no second mode
on `code_mode` that accepts an `id`.

Pi also has an optional tool, `tool_search`. That one loads deferred tools
into the next model turn. It is the slow loop. Do not register it.

The tool description lists `SearchAsync`, `DescribeAsync`, and `InvokeAsync`
the way Pi's `codemode` description lists `searchTools`, `describeTool`,
and `tools.<name>`. That description is the only discovery surface on
`tools/list`.

The language is C# because the Daemon and the host compiler are already
Roslyn (`Microsoft.CodeAnalysis.CSharp` in `DevTools.Execution`). A JavaScript
engine would be a second runtime for the same shape Pi uses only because
Pi's runner is QuickJS.

This is not host `execute_csharp` (`CSharpCodeTool`). That tool compiles an
`IExternalCommand`, loads it in the CAD process, and runs on the host
context. The program runs in the Daemon (`net10.0-windows`). It has no Revit or
AutoCAD API. Its only way to touch a model is `InvokeAsync`, which resolves
the name and uses the existing pipe hop.

Do not reuse `CSharpCompiler` for this tool. That compiler resolves `#r`
and `#load` and looks for `IExternalCommand`. This compile has no
reference resolver and no source resolver. The reference list is fixed:
the BCL, `CatalogScript`, and `ModelContextProtocol`. A further assembly
is not on that list, so the body cannot load one. The body is calls to
`SearchAsync`, `DescribeAsync`, `InvokeAsync`, and `ReadAsync`, plus
ordinary C# over the SDK values those calls return. `#r` and `#load` are
script directives. They are not parsed. Inside the method they are a
syntax error.

- The submitted source is the body of `RunAsync` on a `CatalogScript`
  subclass. `SearchAsync`, `DescribeAsync`, `InvokeAsync`, `ReadAsync`, and
  `CancellationToken` are protected members. The model does not write the
  class or the signature.
- Emit into a collectible load context. Unload after the call.
- No file, network, or process API. No NuGet. No resolver that could add
  a reference while compiling.

Infrastructure tools stay on `tools/list` and are not methods:
`list_machines`, `list_processes`, `launch_host`, `read_file_info`.

| Method | Calls | Returns |
|--------|--------|---------|
| `SearchAsync` | BM25F over the in-memory index. Default limit 8. Default `primitiveType` is `tool`. | `Name`, `Description`, `ProcessId`, `PrimitiveType` |
| `DescribeAsync` | no pipe. Reads the cached tool. | SDK `Tool` |
| `InvokeAsync` | resolves `(processId, tool, name)` then `CallToolPassthroughAsync` | that method's SDK `CallToolResult`, unchanged |
| `ReadAsync` | resolves a resource or template, then the existing read path | SDK `ReadResourceResult`, unchanged |

### What runs

`code` is the body of one method. The model does not write a class, a
signature, or a code fence. The Daemon wraps it:

```csharp
public sealed class Script : CatalogScript
{
    public override async Task<object?> RunAsync()
    {
        // code
    }
}
```

A `return` is the tool result. Locals and anonymous types are allowed. A
class or record is not, because a C# method body cannot declare one. Host
JSON arguments are any object the Daemon serializes
(`new { category = "..." }`). A host payload is not flattened to one
`JsonNode`.

### What the program receives

`InvokeAsync` returns the host `CallToolResult`. `ReadAsync` returns the
host `ReadResourceResult`. `DescribeAsync` returns the SDK `Tool`. These
are the types in `ModelContextProtocol.Protocol`. Nothing is copied onto
a parallel shape.

`CallToolResult` already carries what the host sent:

| Member | Contents |
|--------|----------|
| `StructuredContent` | JSON, or null |
| `Content` | `TextContentBlock`, `ImageContentBlock`, `AudioContentBlock`, `EmbeddedResourceBlock`, `ResourceLinkBlock` |
| `IsError` | the host error flag. The object is still returned. |

Order of `Content` is kept. A screenshot tool with only an image block is
one `ImageContentBlock` and a null `StructuredContent`. Nothing is rewritten
into `{ "text" }`.

`ReadResourceResult.Contents` is `TextResourceContents` or
`BlobResourceContents` (`Uri`, `MimeType`, `Text` or `Blob`). Blob is
base64 for an image or other non-text bytes. `ReadAsync` throws when the
name is a tool.

### What `return` sends back

The outer tool result is a `CallToolResult`. SDK values pass through.

| Program returns | Outer result |
|-----------------|--------------|
| `CallToolResult` | that instance |
| a `ContentBlock`, or a list of them | a `CallToolResult` whose `Content` is those blocks |
| `ReadResourceResult` | one `EmbeddedResourceBlock` per content, the mapping `InvokeTool` uses today |
| `string` | one `TextContentBlock` |
| any other object (`JsonNode`, anonymous type, array) | `StructuredContent`, plus the same JSON in one `TextContentBlock` |

Returning a projected object drops blocks the program did not put on that
object. An image stays an image only when the program returns the
`ImageContentBlock` or the `CallToolResult` that holds it. Base64 is not
copied into `StructuredContent`.

The 1 MiB budget counts the outer result, including base64. Over the budget
is an error and the payload is not sent. The Daemon does not write a temp
file and does not truncate an image.

```csharp
var shot = await InvokeAsync("view_screenshot", null, processId);
return shot.Content.OfType<ImageContentBlock>().First();
```

```csharp
var sheet = await ReadAsync("sheet_preview", null, processId);
return sheet;
```

Handle, in order:

1. Reject an empty `code`.
2. Wrap the body as `Script.RunAsync`. There is no directive pass and no
   `#r` resolver.
3. Compile with Roslyn against the BCL, `CatalogScript`, and
   `ModelContextProtocol`. Collectible load context. No host API reference.
4. If the tool argument `readOnly` is true, `InvokeAsync` resolves the tool
   and throws unless `ReadOnlyHint` is `true`. A missing hint fails closed.
5. `await RunAsync`. `SearchAsync` and `DescribeAsync` do not open a pipe.
   `InvokeAsync` and `ReadAsync` do.
6. Build the outer `CallToolResult` from the return value, by the table
   above. Over 1 MiB is an error and the payload is not sent. Compile
   failures and thrown exceptions are an error result. Earlier host calls
   stay committed.

### Instruction the model sees

This is the whole `code_mode` description. It does not list host tools.
Pi leaves deferred MCP tools out of the description and points the script
at `searchTools`. Schemas stay behind `DescribeAsync`.

```text
Run a C# program that calls host tools. `code` is the body of an async method, not JSON and not a markdown fence. `return` is the only value sent back to you. No Revit or AutoCAD API, no files, no network, no extra assemblies.

Globals:
- await SearchAsync(query, limit: 8, processId: null, primitiveType: null) returns Name, Description, ProcessId, PrimitiveType. PrimitiveType defaults to tool. Values are tool, resource, resource_template.
- await DescribeAsync(name, processId) returns InputSchema, RequiredArgs, ArgsHint, and ReadOnlyHint. Call it before InvokeAsync when the arguments are not obvious.
- await InvokeAsync(name, arguments, processId) returns the SDK CallToolResult. StructuredContent is the JSON. Content holds TextContentBlock, ImageContentBlock, AudioContentBlock, EmbeddedResourceBlock, and ResourceLinkBlock. IsError is the host error flag. Pass processId when SearchAsync shows the same name on more than one process.
- await ReadAsync(name, arguments, processId) returns the SDK ReadResourceResult. Contents are text or blob. Blob is base64 for non-text. It throws if name is a tool.
- return a projected object for JSON. return the ImageContentBlock, AudioContentBlock, CallToolResult, or ReadResourceResult when the model must see that block. A projected object drops blocks you did not copy.
- Filter before return. A return value over 1 MiB fails. That includes image base64.
- list_machines, list_processes, launch_host, and read_file_info are separate tools. Call them directly.
- Calls for one processId run one at a time. A later failure does not undo an earlier InvokeAsync.
```

One system-prompt line. Pi's line names `codemode`. Ours names the tool:

```text
Use code_mode to search, call, and filter host tools in one program, instead of many separate calls.
```

Example under that description:

```csharp
var matched = await SearchAsync("mechanical equipment");
var primitive = matched[0];
var declaration = await DescribeAsync(primitive.Name, primitive.ProcessId);
var raw = await InvokeAsync(primitive.Name, new { category = "Mechanical Equipment" }, primitive.ProcessId);
return raw.AsArray()
    .Where(item => item?["system"] is JsonValue)
    .Select(item => new
    {
        id = item?["id"]?.ToString(),
        family = item?["family"]?.ToString(),
        system = item?["system"]?.ToString(),
        level = item?["level"]?.ToString(),
    })
    .ToArray();
```

`declaration` is there so the program can read `ReadOnlyHint` and
`InputSchema`. Two model turns: write that body, read the array. The host
may still return thousands of elements. They stop at `return`.

A `readOnly` argument on `code_mode` defaults to `false`. When `true`,
`InvokeAsync` refuses a tool whose `ReadOnlyHint` is not `true`. A missing
hint fails closed. The program can also branch on `DescribeAsync`.

`code_mode` is `Destructive=true` and `OpenWorld=true` for the reason in
decision 2. One approval covers every `InvokeAsync` in the program. That is
coarser than approving each host tool. `readOnly: true` blocks writes at
runtime and does not change the hint on `tools/list`.

Host calls for one `processId` stay serialized on that process's external
event. `Task.WhenAll` overlaps only across different processes. A failure
does not roll back earlier calls. Each host tool keeps its own transaction.
The same limit is documented for Pi.

The outer result is a `CallToolResult`, built from the return value by the
table above, under the existing 1 MiB budget. Over the budget is an error
and the payload is not sent. `InvokeAsync` gives the program the host blocks,
including image and audio. The program's `return` decides which of those
blocks the model sees.

Pi, Cursor, and any other MCP client use this same tool. A client that
already has a JavaScript runner does not get a parallel `search_dynamic` /
id-invoke pair. Those two tools on the list are the slow loop. Pi calls
`code_mode` once. The C# inside calls `SearchAsync` and `InvokeAsync` where a
Pi script would call `searchTools` and `tools.<name>`. Large host payloads
stop in the Daemon instead of crossing into QuickJS.

### 4. Refused

- JavaScript, QuickJS, Jint, or any second language runtime.
- A JSON `execute_plan` / tool batch with result references. That is a
  weaker program than the C# body. Resource prefetch stays `ReadAsync`,
  not an MCP `reads[]` argument.
- Keeping `search_dynamic`, or an `id` argument, on the model-facing tool.
  The index stays. The MCP tool does not.
- A second discovery tool beside `code_mode`. `search_dynamic` and
  id-invoke stay off `tools/list`.
- Reusing `CSharpCompiler`, `IExternalCommand`, or host API references for
  the program.
- A result reducer on the inner host passthrough.
- Embeddings, a vector index, or a search NuGet package.
- A synonym or alias list (`AHU`, `HVAC`, stemming).
- Putting `machineId` on search, or changing the `dci2.*` id format.
- Projecting host capabilities onto daemon `tools/list`.

## Alternatives Considered

1. **Keep the weighted sum and only delete the half-match cutoff.** Rejected.
   Removing the cutoff stops some false drops, and it still treats `get` and
   `equipment` as the same +4. IDF is the part that matches Pi's `searchTools`
   and the part 0039 alternative 5 set aside.
2. **BM25 plus a hand-written alias table.** Rejected for this decision. The
   owner asked for BM25. Aliases are a separate recall change with their own
   word list to maintain.
3. **Call a BM25 library.** Rejected. The index is one in-memory dictionary
   rebuilt on catalog change. An in-box formula keeps Daemon's dependency set
   unchanged. 0039's "no dependency" reason still holds; its "recall is
   enough" reason does not.
4. **Set `code_mode` to `Destructive=false`.** Rejected. Clients that
   only read `tools/list` would treat a write capability as a read.
5. **Split `invoke_read` / `invoke_write`.** Rejected. It doubles the
   envelope and still requires the client to pick the right tool from search
   hints. The hints on `SearchItem` are the smaller contract.
6. **Annotations only when `detail=schema`.** Rejected. That forces a second
   search before a read/write decision, which is another model turn in the
   loop this ADR is aimed at.
7. **Leave the program in the client (Pi only).** Rejected. It keeps Cursor
   on one MCP round trip per catalog call.
8. **Keep `search_dynamic` and id-invoke beside `code_mode`.** Rejected.
   The model calls the tools it can see. The pair is the slow loop. Pi does
   not leave `searchTools` on the model-facing tool list either.
9. **`code_mode` accepts either `id` or `code`.** Rejected. The one-id
   case is a short program. A dual contract is how search-then-invoke
   survives.
10. **Tell the model to call host `execute_csharp` instead.** Rejected as
    the orchestration tool. `execute_csharp` is one host process and one
    Revit/AutoCAD API script. The program finds that capability with
    `SearchAsync` and calls it with `InvokeAsync` when the work really is
    host API. The return value of *that* inner call still has to be
    projected by the outer C# or it crosses to the model.
11. **JavaScript inside the Daemon, matching Pi's QuickJS.** Rejected. The
    Daemon already depends on Roslyn. A JS engine would duplicate the
    program runner in a language the rest of the product does not execute.
12. **Delete the search index because the MCP tool is gone.** Rejected.
    `SearchAsync` is Pi's `searchTools`. Without it the program can only
    call ids the model already knew, which means the schemas have to sit
    in the prompt.
13. **Lucene.Net or ElBruno.BM25 for rank.** Rejected. Lucene.Net 4.8 is
    still beta and is a segment index, analyzers, and a query parser for
    a catalog that already lives in a dictionary. ElBruno.BM25 0.5.1
    (July 2026, about 200 downloads) takes one string per document. Its
    readme calls that BM25F, but the published formula is single-field
    BM25 (`k1` default 1.5) with no field weights. Pi's ranker is in-box
    for the same reason. Checked 2026-10-07.
14. **`Microsoft.CodeAnalysis.CSharp.Scripting` as the program host.**
    Rejected as the sandbox. The package is real and matches the pinned
    Roslyn 5.9.0, and a script body with a globals object is the shape of
    `code`. `ScriptOptions.Default` is a REPL: `allowUnsafe`, a source
    resolver for `#load`, a metadata resolver that honors `#r`, and
    references that include the file system. Collectible unload of script
    assemblies is not a stable public guarantee (Roslyn still discusses
    it). The compile stays `CSharpCompilation` from the package already
    referenced, emitted into a collectible load context, with references
    limited to the BCL, `CatalogScript`, and `ModelContextProtocol`. No new
    compiler, and no
    scripting package.

## Consequences

Positive:

- A paraphrased query that shares one real token with a capability remains
  ranked instead of vanishing.
- Common tokens stop tying rare tokens.
- Schema property names participate in rank, matching the postings that
  already exist.
- A program can branch on `ReadOnlyHint` from `DescribeAsync` before
  `InvokeAsync`, in the same run.
- Host parsers stay the source of hints. Search does not re-derive them.
- Every MCP client, including Pi, spends two model turns on a multi-call
  catalog workflow. Large host payloads stay in the Daemon.
- The envelope drops `search_dynamic` and `invoke_dynamic`. The dynamic
  tool is `code_mode` with a single `code` field.

Tradeoffs:

- `ScoringTests.Score_TargetMatchDominates_AndHalfTokenCutoffApplies` encodes
  the cutoff and the +4/+2/+1 scale. Those assertions are replaced, not
  preserved. Relative order becomes IDF- and length-dependent, so tests must
  fix `N`, `df`, and field lengths rather than expect `4` or `0.5`.
- Scores move when any process catalog changes, because `df` and `avg_f`
  are corpus statistics. Two Daemons with different connected hosts can rank
  the same query differently. That is intended.
- Very short catalogs make IDF coarse. With a handful of tools, BM25F is
  close to a weighted tf. The cutoff removal still applies.
- Hints are part of `DescribeAsync`, not `SearchAsync`. Prompts and
  `docs/product/mcp.md` must stop teaching search-then-invoke in the
  same change. The model no longer passes a `dci2.*` id.
- `McpServerJsonContext` already lists `SearchItem`. The new nested record
  must be part of that source-generated graph ([0031](0031-daemon-json-source-gen.md)).
- `code_mode` remains a single destructive tool to MCP permission UI.
  `readOnly: true` is the enforced narrow path. There is no per-id approval.
- The tool compiles on the request path. A cold Roslyn compile is slower
  than one pipe hop and still smaller than an extra model turn. A one-id
  call pays that compile too, because it is a program.
  Repeated calls should reuse a cached compilation only when the source
  is identical; that cache is an implementation detail, not a second
  product.
- One approval covers every invoke inside the program, including a write
  the model did not spell out as its own tool call. `readOnly: true` is
  how a client opts out of that.
- A thrown program leaves earlier host transactions committed.

## Follow-Up

- Implementation plan: [2026-10-07-run-csharp-bm25f](../plans/completed/2026-10-07-run-csharp-bm25f.md).
  Do not start it until this ADR is accepted.
- On acceptance, update `docs/product/mcp.md` and the revit/acad prompts
  that teach search-then-invoke, in the same change as the code. Do not
  copy this formula into `docs/architecture/`. Amend 0027's loop sentence:
  the model calls `code_mode` with C#; search is inside that program.
  Do not weaken the rule that host tools stay off `tools/list`.
- 0039's status already points here as a proposed revision. On acceptance,
  change that pointer from "not yet the rule" to a supersession of decision 9's
  scoring bullet and alternative 5.
- Proof for the implementation change: replace the half-cutoff scoring test;
  add a case where filler tokens do not drop a one-token hit; add a case
  where a high-df token loses to a low-df token; add a case where a
  parameter-name token scores above 0; add a search-response test that
  copies `ReadOnlyHint` onto `DescribeAsync` and omits hints when the host
  tool has none. `SearchAsync` returns `Name`, `Description`, `ProcessId`,
  and `PrimitiveType` only. `code_mode` stays `Destructive=true`. `McpEngine`
  registers `code_mode` and does not register `search_dynamic` or
  `invoke_dynamic`.
- Proof for the program, separate from the ranker tests: a headless
  `code_mode` whose `code` calls `SearchAsync`, `DescribeAsync`, and
  `InvokeAsync` twice, and returns a projection while the host payloads stay
  out of the tool result; a request that still passes `id` fails
  validation; `readOnly: true` rejects a tool with no `ReadOnlyHint`;
  source that contains `#r` fails as a syntax error, because no directive
  resolver runs; a thrown body does not invent a rollback of the first
  `InvokeAsync`.
