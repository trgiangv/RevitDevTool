# Log Output

Logging output is delivered through independent targets. The same trace entry can be shown in the Monitor pane, persisted to a File, or forwarded to an HTTP endpoint.

## Output targets

| Target | Purpose |
| --- | --- |
| **Monitor** | Live output inside RevitDevTool |
| **File** | Persistent `.log` or `.json` records |
| **HTTP** | Batched delivery to a remote endpoint |

These are output mechanisms, not separate logging systems. Log level filtering happens before the selected targets receive the entry.

## Monitor

The Monitor pane is the interactive output target. Open the Trace Log panel from the host UI, start the listener, and use the level filter to focus on the messages you need.

## File

File output is useful for persistent diagnostics, support bundles, and startup investigation. It can write text or JSON records to the configured log folder. Startup failures are recorded separately as `crash_{app}_{ver}_{pid}.log` before the normal logging pipeline is available.

## HTTP

Enable HTTP output when another service should receive the log stream. RevitDevTool batches entries and sends one POST per batch to the configured endpoint.

| Format | Content type | Body |
| --- | --- | --- |
| `Json` | `application/x-ndjson` | One JSON entry per line |
| `Text` | `text/plain` | Plain-text lines separated by newlines |

HTTP errors do not stop the local logging pipeline. The remote endpoint must accept the configured format.

## Choosing an output

- Use **Monitor** while actively running a script or add-in.
- Use **File** when the result must survive a host restart.
- Use **HTTP** for centralized collection or external monitoring.
- Enable more than one target when the same run needs both local and remote diagnostics.

## Related

- [Log Level](/docs/logging/Logging-Overview) — filtering and standard trace APIs
- [Geometry Visualization](/docs/logging/Visualization-Overview) — trace geometry in Revit
