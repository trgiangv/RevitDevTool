# Logging Visualization Review

Use when editing logging sinks, trace listeners, monitor output, geometry visualization, or host rendering integration.

## Checklist

- Read `docs/Logging/README.md`, `docs/Visualization/README.md`, and `docs/ai/host-boundaries.md`.
- Keep shared logging host-neutral where possible.
- Revit geometry routing through `GeometryListener` and DirectContext3D belongs in the Revit host.
- Other hosts should add rendering adapters instead of coupling shared logging to Revit APIs.
- Preserve text logging behavior for non-geometry trace output.
- Update `docs/Logging/README.md`, `docs/Visualization/README.md`, or `docs/ai/host-boundaries.md` when changing logging flow, visualization routing, or host rendering responsibilities.
- Verify with a host build and collect logs when behavior changes.
