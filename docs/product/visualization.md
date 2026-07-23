# Visualization Product Contract

Transient DirectContext3D overlays for Revit only. Not a shared multi-host
rendering stack.

## Behavior

- Implementation lives under `source/RevitDevTool/Visualization/`.
- Server-per-geometry-type with opaque + transparent passes.
- Logging geometry listeners may feed visualization; shared libs must not take a
  DirectContext3D dependency.

## Related

- Architecture: [`docs/architecture/Visualization/README.md`](../architecture/Visualization/README.md)
- Host boundaries: [`docs/agents/host-boundaries.md`](../agents/host-boundaries.md)
