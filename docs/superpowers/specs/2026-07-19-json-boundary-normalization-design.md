# JSON Boundary Normalization Design

**Status:** Approved for implementation

**Date:** 2026-07-19

## Problem

MCP Runtime V2 exposed a valid JSON Schema union, `"type": ["string", "null"]`, to `McpRegistryViewModel`. The UI called `JsonElement.GetString()` without first validating the element kind and raised an unhandled `InvalidOperationException` during Revit startup. The same failure pattern exists at other JSON boundaries: individual call sites combine property lookup, kind assumptions, coercion, fallback, and logging in different ways.

The current inventory contains 25 direct `TryGetProperty` calls: 11 in production and 14 in tests. Some are guarded correctly, some are only partially guarded, and none are protected by a repository-wide rule that prevents the unsafe pattern from returning.

## Goals

1. Remove direct `TryGetProperty` calls from feature, domain, UI, daemon, MCP, execution, and test code.
2. Keep exactly one low-level `TryGetProperty` implementation in a shared JSON reader.
3. Normalize missing-property, wrong-kind, null, malformed-value, and explicit coercion behavior.
4. Make every external JSON boundary choose an explicit fallback or error response and emit one structured log when a failure is operationally relevant.
5. Support JSON Schema `type` strings, union arrays, `anyOf`, `oneOf`, nullable schemas, absent metadata, and unknown extension keywords without throwing.
6. Isolate catalog primitive failures so one malformed external tool cannot abort the whole MCP catalog.
7. Await and observe MCP Registry initialization so initialization failures do not become unobserved dispatcher task faults.
8. Add regression, boundary, integration, architecture-policy, and net48 build coverage.

## Non-goals

- Do not add a general JSON Schema validation package.
- Do not replace ModelContextProtocol SDK protocol models with repository-owned wire DTOs.
- Do not log complete external payloads, source code, credentials, tokens, or package-index responses.
- Do not change direct pytest framing, routes, or contracts.
- Do not move Revit- or AutoCAD-specific behavior into shared libraries.
- Do not make Python dependency installation or network work block host startup.

## Chosen Architecture

### 1. Shared, non-logging JSON reader

Create `DevTools.Utilities.Json.JsonElementReader`. It is the only production type allowed to call `JsonElement.TryGetProperty`.

The reader returns values through a structured result instead of throwing for input-shape errors:

```csharp
public enum JsonReadErrorCode
{
    MissingProperty,
    WrongValueKind,
    NullNotAllowed,
    InvalidValue
}

public sealed record JsonReadError(
    JsonReadErrorCode Code,
    string Path,
    string Expected,
    JsonValueKind ActualKind,
    string Message);

public readonly struct JsonReadResult<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public JsonReadError? Error { get; }
}

public enum JsonNumberCoercion
{
    None,
    AllowString
}
```

`JsonElementReader` provides these operations:

```csharp
ReadRequiredProperty(JsonElement parent, string propertyName, JsonValueKind expectedKind, string parentPath = "$")
ReadOptionalProperty(JsonElement parent, string propertyName, JsonValueKind expectedKind, string parentPath = "$")
ReadRequiredString(JsonElement parent, string propertyName, string parentPath = "$")
ReadOptionalString(JsonElement parent, string propertyName, string parentPath = "$")
ReadRequiredNonEmptyString(JsonElement parent, string propertyName, string parentPath = "$")
ReadOptionalBoolean(JsonElement parent, string propertyName, string parentPath = "$")
ReadOptionalInt32(JsonElement parent, string propertyName, JsonNumberCoercion coercion, string parentPath = "$")
ReadString(JsonElement value, string path, bool allowNull = false)
ReadBoolean(JsonElement value, string path)
ReadInt32(JsonElement value, string path, JsonNumberCoercion coercion)
```

Equivalent dictionary overloads accept `IReadOnlyDictionary<string, JsonElement>?` for MCP tool arguments. Missing optional values return successful results containing `null`. Missing required values and present values of the wrong kind return failed results with a stable error code and JSON path.

Every element-property method delegates to one private `ReadPropertyCore` method. `ReadPropertyCore` contains the reader's single `TryGetProperty` call; required, optional, and typed operations must not duplicate it.

The reader never logs. This keeps the utility layer host-neutral and prevents duplicate logs when a result passes through multiple layers.

### 2. Structured logging at boundaries

Add `DevTools.Logging.Extensions.JsonReadLoggingExtensions` and a stable `EventId` for JSON boundary failures. Boundaries call:

```csharp
logger.LogJsonReadDiagnostic(level, operation, error, sourceKind);
```

The structured event contains `Operation`, `Code`, `Path`, `Expected`, `ActualKind`, and `SourceKind`. It does not contain the raw payload. The explicit `LogLevel` argument prevents the extension from guessing domain severity. A boundary logs at most once for one rejected request or response. Expected absence of optional properties is not logged.

Severity rules:

- `Debug`: supported fallback or unsupported optional JSON Schema display shape.
- `Warning`: invalid external response, malformed configured tool metadata, invalid control-pipe request, or invalid MCP tool argument.
- `Error`: catalog initialization cannot produce any usable snapshot or an internal invariant is broken.

### 3. MCP JSON Schema formatter

Create `DevTools.Mcp.Schema.JsonSchemaArgumentFormatter`. Presentation passes `Tool.InputSchema` directly as a `JsonElement`; it no longer serializes the element to text and reparses it.

The formatter returns:

```csharp
public sealed record JsonSchemaFormatResult(
    string Text,
    IReadOnlyList<JsonReadError> Diagnostics);

public static JsonSchemaFormatResult Format(JsonElement inputSchema);
```

Formatting rules:

- `"type": "string"` displays `string`.
- `"type": ["string", "null"]` displays `string | null`.
- `anyOf` and `oneOf` recursively collect unique types in declaration order.
- A missing type displays `any`.
- A malformed type, title, or description records a diagnostic and falls back to `any`, the property name, or no description.
- Unknown JSON Schema keywords are ignored by the display formatter and do not produce failures or diagnostics.
- No schema shape can throw from tooltip construction.

`McpRegistryViewModel` logs formatter diagnostics with the tool name and source address, completes the full tool list, and exposes catalog diagnostics as before.

### 4. Boundary migrations

All 11 production `TryGetProperty` call sites migrate to the reader or schema formatter:

- Pixi package-list parsing.
- Pip package-list parsing.
- PyPI and Conda latest-version responses.
- Daemon control-pipe method parsing.
- Daemon status parsing in Presentation.
- MCP Registry JSON Schema formatting.

The daemon MCP tools also migrate unguarded scalar getters even though they do not currently call `TryGetProperty`:

- `launch_host` string arguments.
- `open_model` strings and `hostId`; `hostId` intentionally preserves numeric-string compatibility through `JsonNumberCoercion.AllowString`.
- `read_file_info.filePath`.

Invalid daemon tool input returns `ToolHelpers.ErrorResult` with a concise field-specific message and emits one structured warning. It does not escape as `InvalidOperationException`.

Pip parsing becomes an injected service with `ILogger<PipPackageHelper>` rather than a static helper so failures use the same logging contract as Pixi parsing. Malformed list entries are skipped individually; one bad entry does not discard valid entries. A malformed root payload returns an empty list and one warning.

### 5. MCP catalog and UI initialization resilience

`McpCatalogLoader.BuildPrimitives` catches failures per registration. It adds a `primitive_build_failed` diagnostic, logs the primitive kind/key/provider, and continues building the snapshot. Duplicate and identity behavior remains unchanged.

`McpRegistryView` replaces the unobserved `Dispatcher.BeginInvoke(viewModel.InitializeAsync)` delegate with a one-shot async `Loaded` handler that awaits `InitializeAsync` inside `try/catch`. `McpRegistryViewModel` records an initialization diagnostic/status and the view logs the terminal failure. A catalog refresh that contains partial diagnostics still completes normally.

Catalog ownership and lazy-loading behavior remain unchanged in this change. The plan must not introduce Python dependency resolution, package installation, or network work into `Host.Start()`.

### 6. Test-side JSON assertions and source policy

Create a server-test `JsonAssert` helper that uses `JsonElementReader` for required/optional property assertions. Migrate all 14 direct test call sites to this helper.

Add an architecture-policy test that scans `source/**/*.cs` and `tests/**/*.cs` from the repository root. The test constructs the forbidden token as `"TryGet" + "Property("` so it does not match itself. Exactly one occurrence is allowed:

```text
source/DevTools.Utilities/Json/JsonElementReader.cs
```

Any future direct call fails the test and reports its file and line.

## Error Flow

```text
external JSON / SDK schema
  -> JsonElementReader
  -> JsonReadResult<T>
     -> success: domain operation continues
     -> failure: boundary chooses fallback or protocol error
                 + one structured, payload-free log
```

For JSON Schema display:

```text
Tool.InputSchema
  -> JsonSchemaArgumentFormatter
  -> complete tooltip text + diagnostics
  -> UI logs diagnostics and renders every remaining tool
```

For catalog construction:

```text
registration
  -> primitive adapter
     -> success: add primitive
     -> exception: primitive_build_failed diagnostic + continue
```

## Test Strategy

### Shared reader tests

Cover required and optional properties, missing values, null handling, every supported scalar kind, wrong kinds, non-empty strings, nested JSON paths, integer overflow, numeric-string coercion enabled/disabled, object/array properties, and dictionary argument overloads.

### Schema formatter tests

Cover scalar type, nullable union type, multiple union members, `anyOf`, `oneOf`, duplicate alternatives, missing type, absent title/description, malformed metadata kinds, wrong root kind, missing/wrong `properties`, and the exact SDK-generated `execute_python_code` schema.

### Boundary tests

- Pixi and Pip keep valid entries while reporting malformed entries.
- PyPI/Conda wrong response kinds return `null` and log one structured event.
- Control-pipe non-string method returns `invalid_request` without throwing.
- Daemon status wrong kind returns `false` without throwing.
- `launch_host`, `open_model`, and `read_file_info` wrong argument kinds return MCP errors without throwing.
- A throwing primitive adapter does not prevent later primitives from loading and produces `primitive_build_failed`.
- MCP Registry initialization completes its list when one tool contains a union or malformed display schema.
- The source-policy test permits only the central reader call.

### Compatibility and integration verification

Run focused net10 tests, the full .NET test wrapper, and host builds for Autodesk 2024 and 2027. The 2024 build proves net48 compatibility; the 2027 build proves the current SDK path. Live named-pipe and Revit startup verification records whether the registry renders all four built-ins, whether the daemon receives the refreshed catalog, and whether logs contain structured diagnostics without an unhandled dispatcher exception.

## Documentation

- Update `docs/MCP/README.md` with schema normalization, primitive failure isolation, and UI initialization behavior.
- Update `docs/agents/mcp-pytest-bridge.md` with the new focused verification and diagnostic code.
- Add `docs/Architecture/JsonBoundaries.md` as the repository-wide rule for safe JSON extraction and boundary logging.
- Route future JSON-boundary work to that document from `docs/agents/index.md`.

## Acceptance Criteria

1. The source-policy test finds exactly one `TryGetProperty` call in the whole `source/` and `tests/` trees.
2. The exact `execute_python_code` schema formats successfully as `string | null` and cannot fault MCP Registry initialization.
3. Invalid JSON kinds at migrated production boundaries never escape as `InvalidOperationException`.
4. Every rejected external request/response produces either a protocol-safe error or an explicit fallback and at most one structured, payload-free log.
5. One broken external primitive cannot prevent valid primitives from entering the catalog.
6. MCP Registry initialization is awaited and observed; no unobserved dispatcher task is used.
7. Duplicate identities, configured path persistence, invalid-path pruning, Broker/Native surfaces, SDK protocol semantics, and the direct pytest lane remain unchanged.
8. Focused tests, full .NET tests, `Debug.Autodesk.2024`, and `Debug.Autodesk.2027` builds pass, or an environmental blocker is documented with its exact command and output.
