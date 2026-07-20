# Pytest MCP execution boundary

The shared execution layer provides the in-host half of pytest execution. It
does not collect tests and it does not own Python plugin discovery, retries, or
pytest reporting. `RevitDevTool.PyTest` collects locally and calls the host's
reserved `pytest_run` MCP tool over the canonical named pipe.

`PytestDependencyService` prepares declared dependencies before
`PytestExecutionService` enters the host-safe execution path. The embedded
`PytestRunner.py` runs selected node IDs in the host Python runtime and returns
typed `PytestRunResponse` domain data. `PytestRunTool` maps infrastructure
failures to stable MCP error codes and emits token-scoped standard progress;
case events are optional negotiated MCP notifications.

There is one host data-plane transport: standard MCP on
`DevTools_{Host}_{Version}_{PID}`. The former framed pytest bridge has no
compatibility decoder or fallback. This document supersedes historical prose
that described direct bridge envelopes.
