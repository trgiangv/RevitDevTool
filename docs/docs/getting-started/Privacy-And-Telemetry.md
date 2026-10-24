# Privacy & Telemetry

RevitDevTool includes optional, anonymous telemetry to help improve the platform. This page describes exactly what is collected, what is never collected, and how to opt out.

## Purpose

Telemetry is used strictly for **development and quality improvement**:

- Identify critical crashes and unhandled exceptions across different host environments
- Understand which execution modes are actively used (Python, C#, F#, IronPython)
- Prioritize stability and performance work based on real-world usage patterns

Telemetry is **not used** for user profiling, advertising, or any commercial data collection.

## What Is Collected

### Operations

Each script/assembly run, each MCP tool call, and each in-host test run is sent as one timed operation. It contains only:

| Field | Example |
|-------|---------|
| Operation | `devtool.execution`, `devtool.mcp`, `devtool.test` |
| Provider | `Python`, `CSharp`, `IronPython`, `Dotnet`, `BuiltIn`, `pytest`, `ipytest`, `NUnit`, `MSTest`, `TUnit` |
| Status | ok, cancelled, failed |
| Duration | 420 ms |

No script names, tool names, parameters, or output are included.

### Sessions

The host process reports session start and end (release health), so crash-free rates can be measured per host and DevTool version.

### Errors and Crashes

An error report is sent only when a failure starts in RevitDevTool code. It contains:

- Exception type and message
- Stack trace (file names only, no directories)
- Host environment (Revit/AutoCAD version, DevTool version)

Errors in your own scripts, commands, MCP toolsets, or tests are **not** sent. They count only as a failed operation; failing tests count as a completed run. Visualization errors are sent at most once per error type and geometry server per session. Cancellations and timeouts are never sent. Exceptions from Revit, AutoCAD, or other add-ins are ignored. Absolute paths are removed from all text before sending, and the machine name is never sent.

## What Is Never Collected

- BIM model content, geometry data, or project files
- Script source code or file contents
- Log message text or output content
- MCP tool names or parameters
- Document names, file paths, or project-specific data
- Windows username, email, or personal identity
- Network configuration or IP-based geolocation

## User ID

The user ID identifies one Windows user on one machine. It is a keyed one-way hash (HMAC-SHA256) of the Windows machine GUID and the account SID. The raw machine GUID, SID, and user name are never sent, and the hash cannot be reversed or matched with IDs from other applications. Nothing is stored on disk. The ID changes only when Windows is reinstalled or the account changes. If either value cannot be read, a random ID is used for that session only.

The ID is used solely to correlate reports from the same user and to count users. It is **not** linked to any user account, email, or Autodesk account.

## How To Opt Out

Open RevitDevTool settings and disable **"Anonymous diagnostics"**. When disabled, no telemetry data is sent. The change takes effect on the next host restart.

When telemetry is disabled, no network requests are made to the telemetry service.

## Data Storage

Telemetry data is processed by [Sentry](https://sentry.io/) hosted in the **European Union (Germany)**. Data is subject to Sentry's data retention policies and GDPR compliance.

No telemetry data is stored locally.
