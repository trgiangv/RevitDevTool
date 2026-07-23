# MCP Workflow Patterns

Practical patterns for AI agents using RevitDevTool MCP tools.

---

## Workflow: Code Execution (Create/Read/Update/Delete)

```mermaid
flowchart LR
    A[Read API cheatsheet] --> B[Read model context]
    B --> C[Generate IExternalCommand code]
    C --> D[call_dynamic_tool execute_csharp_code]
    D -->|Success| E[Verify result]
    D -->|Compilation Error| C
    D -->|Runtime Error / Rollback| F[Read warnings → fix → retry]
```

### Key Points

- Always read `revit://api-cheatsheet` before first code generation (saves retries)
- Read `revit://model/context` for live state (levels, categories, active view)
- Error responses are categorized: `[COMPILATION ERROR]`, `[RUNTIME ERROR]`, `[ROLLBACK]`
- Rollback means ExecutionGuard resolved a constraint violation automatically

---

## Workflow: Vision (Verify Visual Results)

```mermaid
flowchart LR
    A[Execute code changes] --> B[Read revit://view/screenshot]
    B --> C{AI inspects image}
    C -->|Looks correct| D[Done]
    C -->|Issues found| E[Undo + retry]
```

### Key Points

- `revit://view/screenshot` returns 1920px PNG of the active view
- Size changes between calls indicate model state actually changed
- Use after creating geometry to confirm placement
- Non-exportable views (Project Browser, Templates) return text error

---

## Workflow: Undo (Recover from Mistakes)

```mermaid
flowchart LR
    A[Execute code] --> B{Result OK?}
    B -->|Yes| C[Continue]
    B -->|No| D[call navigate_history direction=back steps=1]
    D --> E[Verify model state]
    E --> F[Retry with corrected code]
```

### Key Points

- `navigate_history` unified contract: `direction="back"|"forward"`, `steps=N`
- Same tool name on both Revit and AutoCAD — routed via `hostInstanceId` when multi-host
- Revit: synchronous, returns exact stack state
- AutoCAD: async queue, returns estimated stack state
- Undo stack is per-document, not per-session

---

## Workflow: Full Development Loop

```mermaid
flowchart TD
    Start[Task received] --> Read[Read resources: cheatsheet + context + version]
    Read --> Plan[Plan implementation]
    Plan --> Code[Generate and execute code]
    Code --> Check{Success?}
    Check -->|Error| Fix[Read error details → fix → retry]
    Fix --> Code
    Check -->|Success| Verify[Read screenshot]
    Verify --> Visual{Looks right?}
    Visual -->|No| Undo[navigate_history direction=back]
    Undo --> Code
    Visual -->|Yes| Done[Report success]
```

---

## Token Efficiency Tips

1. **Read cheatsheet once per session** — cache it mentally, don't re-read every call
2. **Read model context before each operation** — it's live and cheap (~200 tokens)
3. **Use `refresh_dynamic_catalog` only after host reconnects** — not between every call
4. **Structured errors save retries** — read the error category before regenerating code
5. **Screenshot is binary** — won't consume text tokens, but verify you have vision capability

---

## Multi-Instance Scenarios

When multiple hosts connect (e.g., Revit 2025 + Revit 2024), use `list_dynamic_tools` to see all registrations with their `hostInstanceId`, then pass `hostInstanceId` (PID) to disambiguate:

```json
{
  "name": "execute_csharp_code",
  "arguments": {"code": "..."},
  "hostInstanceId": 12345
}
```

---

## Related

- In-host dispatch: `docs/architecture/Execution/mcp-dispatch.md`
- Daemon architecture: `docs/architecture/MCP/daemon.md`
- Transport modes: `docs/architecture/MCP/transport.md`
