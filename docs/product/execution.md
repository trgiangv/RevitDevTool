# Execution Product Contract

Agents and hosts can execute .NET assemblies, Python, IronPython, F#, and C#
scripts inside the active host context through the shared execution engine.

## Behavior

- Execution is host-adapted via abstractions (`IHostContextExecutor` and related
  bridges); shared logic stays in `DevTools.Execution*`.
- Host main-thread access is required where the host API demands it; work is
  serialized through the host executor queue.
- PEP 723 script dependencies are resolved before Python execution when a
  provider is ready. Host-embedded interpreters install into the uv sidecar
  and `site.addsitedir` that prefix onto the live interp
  ([python-runtime.md](../architecture/Execution/python-runtime.md)).
- MCP and pytest bridges reuse the same execution/dispatch path rather than
  inventing parallel runners.

## Non-Goals

- Does not replace host-specific UI/threading rules.
- Does not claim deep end-to-end coverage from smoke tests alone.

## Related

- Architecture: [`docs/architecture/Execution/README.md`](../architecture/Execution/README.md)
- Agent digest: [`docs/agents/execution-system.md`](../agents/execution-system.md)
