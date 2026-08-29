# Logging Product Contract

Unified multi-sink logging for host and shared platform code, including console
and Python `print` redirection when configured.

## Behavior

- Sinks may include monitor, file, and HTTP destinations.
- Host context enrichment is allowed; geometry interception can route to
  visualization on Revit.
- If RevitDevTool or AcadDevTool throws during add-in startup (or an unhandled
  exception fires while startup trace is active), a dump is written to
  `%APPDATA%\RevitDevTool\{Year}\Logs\crash_{app}_{ver}_{pid}.log` with coarse
  milestones and the exception. Successful startup creates no crash file. Rolling
  session logs stay `log_*` via FileLogProcessor.
- Logging must not become a substitute for executable product proof.

## Related

- Architecture: [`docs/architecture/Logging/README.md`](../architecture/Logging/README.md)
