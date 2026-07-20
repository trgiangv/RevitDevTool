# C# Boilerplate Injection for execute_csharp_code

**Status**: Draft  
**Date**: 2026-07-19  
**Scope**: Token efficiency optimization — server-side IExternalCommand template wrapping

## Problem

`execute_csharp_code` requires the AI agent to send a full `IExternalCommand` class with all boilerplate (usings, Transaction attribute, class declaration, Execute method wrapper, `commandData`/`doc` preamble). This boilerplate consumes ~800 tokens per execution — about 40% of the total code sent. Over a roundtrip (query + create + verify), boilerplate waste is ~2,400 tokens.

## Solution

Add a `wrap` mode to `execute_csharp_code`. When enabled, the server wraps the user's minimal code in a fixed IExternalCommand template. The AI sends only `#r` directives, `using` directives, and the inner logic body.

### Target Token Reduction

| Scenario | Before | After | Reduction |
|----------|--------|-------|-----------|
| Query walls + levels | ~1,800 | ~600 | 67% |
| Create element | ~2,200 | ~800 | 64% |
| Full roundtrip (query+create+verify) | ~13,000 | ~8,000 | 38% |

## Schema Change

`execute_csharp_code` gains two optional parameters:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `wrap` | bool | `false` | When `true`, wraps user code in IExternalCommand template |
| `transaction` | string | `"Manual"` | Transaction mode: `"Manual"` or `"ReadOnly"` (only effective when `wrap: true`) |

Backward compatibility: when `wrap` is absent or `false`, behavior is identical to the current implementation.

### Example Input (wrap mode)

```json
{
  "code": "#r \"nuget: Clipper2, 2.0.0\"\nusing Autodesk.Revit.DB;\n\nvar walls = new FilteredElementCollector(doc).OfClass(typeof(Wall)).GetElementCount();\nmessage = $\"Total: {walls}\";",
  "wrap": true,
  "transaction": "ReadOnly"
}
```

## Server-Side Template

```csharp
// Fixed usings (always included by server)
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

// Slot 1: User-provided using directives (deduplicated against fixed usings)
{user_usings}

// Slot 2: Transaction attribute from the 'transaction' parameter
[Transaction(TransactionMode.{transaction})]
public class Command : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        // Fixed preamble
        var uiDoc = commandData.Application.ActiveUIDocument;
        var doc = uiDoc.Document;

        // Slot 3: User inner code body
#line 1 "user_code.cs"
        {user_code}
#line default

        return Result.Succeeded;
    }
}
```

## Code Extraction Rules

The `CSharpCodeWrapper` class extracts three segments from the user's code string:

1. **`#r` directives**: Lines starting with `#r` are preserved at the top (as today). These are consumed by `CSharpDirectiveParser` before compilation — no change to existing behavior.

2. **`using` directives**: Lines starting with `using ` are collected, deduplicated against the fixed server usings, and placed in slot 1.

3. **Body**: All remaining lines form slot 3 (the Execute method body).

The `message` ref parameter: if the user code assigns to `message`, that assignment is preserved in the body. If the user does not assign `message`, it remains the default empty string (or whatever the preamble sets). No automatic message generation is added.

## Pipeline Integration

**Current pipeline:**
```
CSharpCodeTool.ExecuteAsync()
  → CSharpDirectiveParser.ResolveGraph()
  → CSharpCompiler.CompileAsync()
```

**New pipeline (wrap mode):**
```
CSharpCodeTool.ExecuteAsync()
  → if wrap: CSharpCodeWrapper.Wrap(code, transaction)
  → CSharpDirectiveParser.ResolveGraph()    // unchanged
  → CSharpCompiler.CompileAsync(wrapped)    // unchanged
```

The wrap step is inserted before directive parsing. No changes to `CSharpCompiler`, `CSharpDirectiveParser`, `CSharpCompilationCache`, `CSharpCodeTool` schema parsing, or `RevitCompiledScriptBridge`.

### New File

`source/DevTools.Execution/Providers/CSharp/CSharpCodeWrapper.cs`

- Single static method: `Wrap(string code, string transactionMode) → string`
- Pure function, no dependencies on host API or MCP infrastructure

### Modified File

`source/DevTools.Execution/External/Mcp/BuiltIn/CSharpCodeTool.cs`

- Read `wrap` and `transaction` from tool arguments (with defaults)
- Call `CSharpCodeWrapper.Wrap()` before passing code to compiler

## Error Handling

### Line Number Mapping

The template uses Roslyn's `#line` directive to remap compilation error line numbers back to the user's original code:

```
#line 1 "user_code.cs"
    {user_code}
#line default
```

Compilation errors will reference `user_code.cs:N` instead of the wrapped code line number. Example:

```
Before (no mapping): error CS0103 at line 22 (in wrapped code)
After (with mapping): error CS0103 at user_code.cs:5 (in user's original code)
```

### Conflict with User #line

If the user's own code contains `#line` directives, the server's `#line default` will end the mapping. This is an unsupported edge case — the recommendation is to avoid `#line` in user code when using wrap mode.

## Backward Compatibility

- `wrap` defaults to `false` — existing callers are unaffected
- When `wrap` is absent from the arguments, behavior is identical to today
- `transaction` is ignored when `wrap` is `false`
- All existing tests continue to pass without modification

## Testing

### New Tests (in `DevTools.Execution.Tests`)

| Test | Description |
|------|-------------|
| `Wrap_BasicCode` | Minimal code with no usings compiles and runs |
| `Wrap_WithUsings` | User usings are extracted and deduplicated against fixed usings |
| `Wrap_WithNuGet` | `#r` directives are preserved and NuGet packages resolved |
| `Wrap_ReadOnlyTransaction` | `transaction: "ReadOnly"` produces `TransactionMode.ReadOnly` |
| `Wrap_ManualTransaction` | `transaction: "Manual"` produces `TransactionMode.Manual` |
| `Wrap_CompilationError_LineNumber` | Error at user line 3 reports line 3, not template line |
| `Wrap_NoMessageAssignment` | Code without `message =` assignment compiles and runs |
| `BackwardCompat_NoWrap` | Full IExternalCommand with `wrap: false` works unchanged |
| `BackwardCompat_MissingWrap` | Full IExternalCommand without `wrap` parameter works unchanged |

### Roundtrip Integration Test

A live Revit roundtrip test in wrap mode:
1. `execute_csharp_code(wrap:true)` — query wall count
2. `execute_csharp_code(wrap:true)` — create test wall
3. `navigate_history(back, 1)` — undo
4. `execute_csharp_code(wrap:true)` — verify wall count restored

## Files Changed

| File | Change |
|------|--------|
| `source/DevTools.Execution/Providers/CSharp/CSharpCodeWrapper.cs` | **New**: wrapping logic |
| `source/DevTools.Execution/External/Mcp/BuiltIn/CSharpCodeTool.cs` | Read `wrap`/`transaction` params, call wrapper |
| `source/DevTools.Execution.Tests/...` | Add 9 tests |

## Limitations

- **Using extraction is line-based**: Lines starting with `using ` are extracted regardless of context. A `using` keyword inside a string literal, comment, or as a `using var` statement would be incorrectly extracted. This matches the existing `#r` extraction behavior in `CSharpDirectiveParser`.
- **No `return` in body**: The template always appends `return Result.Succeeded;`. User code in wrap mode should not contain `return` statements — doing so creates unreachable code (standard C# compilation error).
- **`doc`/`uiDoc` are pre-declared**: The template declares `var uiDoc = ...` and `var doc = ...`. User code should use these variables directly; re-declaring them causes a compilation error.

## Out of Scope

- Snippet/template library (separate spec)
- Persistent disk compilation cache (keeping in-memory, per user decision)
- Python execution optimization (Python already has lower boilerplate overhead)
- `devtools_search` → `devtools_invoke` shortcut optimization
- Multi-host addin deployment automation
