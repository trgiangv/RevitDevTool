# Privacy & Telemetry

RevitDevTool includes optional, anonymous telemetry to help improve the platform. This page describes exactly what is collected, what is never collected, and how to opt out.

## Purpose

Telemetry is used strictly for **development and quality improvement**:

- Identify critical crashes and unhandled exceptions across different host environments
- Understand which execution modes are actively used (Python, C#, F#, IronPython)
- Prioritize stability and performance work based on real-world usage patterns

Telemetry is **not used** for user profiling, advertising, or any commercial data collection.

## What Is Collected

### Session Usage Summary

One aggregated event is sent when the host shuts down. It contains only **counts** — no content:

| Category | What is counted | Example |
|----------|-----------------|---------|
| Execution | Number of script/assembly runs by mode | "Python: 12, C#: 3, IronPython: 0" |
| MCP | Number of MCP tool dispatches by backend type | "Assembly: 5, Python: 2" |
| Visualization | Geometry rendering calls by type (Revit only) | "curve: 8, solid: 2" |
| Logging | Log lines by severity level | "Info: 45, Warning: 3, Error: 1" |

No script names, tool names, log messages, or file paths are included in usage summaries.

### Critical Exceptions

When an unhandled crash occurs, an exception report is sent containing:

- Exception type and message
- Stack trace
- Host environment (Revit/AutoCAD version, DevTool version)

Exception reports help diagnose crashes that users may not report manually. Routine errors like cancellations and timeouts are filtered out and never sent.

## What Is Never Collected

- BIM model content, geometry data, or project files
- Script source code or file contents
- Log message text or output content
- MCP tool names or parameters
- Document names, file paths, or project-specific data
- Windows username, email, or personal identity
- Network configuration or IP-based geolocation

## Installation ID

A random UUID is generated once per machine and stored locally (`%AppData%\RevitDevTool\installation_id.txt`). This ID is used solely to correlate crash reports from the same installation — it is **not** linked to any user account, Windows identity, or Autodesk account.

## How To Opt Out

Open RevitDevTool settings and disable **"Anonymous diagnostics"**. When disabled, no telemetry data is sent. The change takes effect on the next host restart.

When telemetry is disabled, no network requests are made to the telemetry service.

## Data Storage

Telemetry data is processed by [Sentry](https://sentry.io/) hosted in the **European Union (Germany)**. Data is subject to Sentry's data retention policies and GDPR compliance.

No telemetry data is stored locally beyond the installation ID file.
