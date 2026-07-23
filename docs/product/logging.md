# Logging Product Contract

Unified multi-sink logging for host and shared platform code, including console
and Python `print` redirection when configured.

## Behavior

- Sinks may include monitor, file, and HTTP destinations.
- Host context enrichment is allowed; geometry interception can route to
  visualization on Revit.
- Logging must not become a substitute for executable product proof.

## Related

- Architecture: [`docs/architecture/Logging/README.md`](../architecture/Logging/README.md)
