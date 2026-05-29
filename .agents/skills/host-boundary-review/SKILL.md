# Host Boundary Review

Use when changing shared platform code, adding host support, or moving code between host projects and `DevTools.*`.

## Checklist

- Shared `DevTools.*` libraries should depend on host abstractions, not Revit/AutoCAD/Tekla/Bentley APIs.
- Put host API calls, threading, transactions, document context, and rendering adapters in host projects.
- Prefer adding a host adapter implementation over adding platform-specific branches in shared services.
- Check whether Revit and AutoCAD both need a registration update.
- For new hosts, add a host project and packaging path rather than expanding Autodesk-specific assumptions.
- Because host integration tests are shallow, explicitly reason about threading, document context, and packaging impact in the final notes when tests cannot cover them.
- Update `docs/ai/host-boundaries.md` and the relevant module README when introducing a new host boundary, adapter responsibility, or platform split.
- Verify with the narrowest affected host build.
