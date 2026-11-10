---
sidebar_position: 3
---

# Upgrade from 3.x to 4

RevitDevTool 4 changes how it installs and updates, how .NET tests run, and how an AI client talks to a host.
This page lists what to do when you move from `3.x` to `4.0.0-beta.8` or later. For every change, see
[Release Notes](/docs/getting-started/Release-Notes).

## What changes

| Area | 3.x | 4 |
| --- | --- | --- |
| Installer | Inno Setup, with an uninstall entry in Windows **Apps** | `RevitDevTool-Setup.exe` from a `daemon/v...` release; no administrator rights |
| Add-in | One `RevitDevTool.bundle` for every host | `RevitDevTool.bundle` for Revit and `AcadDevTool.bundle` for the AutoCAD family; only the hosts you select |
| Daemon and MCP server | Inside the add-in bundle | `%LOCALAPPDATA%\RevitDevTool\current\DevTools.Daemon.exe` |
| Updates | Download and run a new installer | **Updates** tab in the Daemon window; the Daemon and the add-in update separately |
| Versions | One version for everything | Four products with their own versions: Daemon, add-in, `RevitDevTool.TestAdapter`, `revitdevtool-pytest` |
| MCP tools | `list_host_instances`, `launch_host`, `open_model`, `read_file_info` | `list_processes`, `launch_host`, `read_file_info`, `code_mode` |
| .NET tests | Not available | NUnit, TUnit, or MSTest through `RevitDevTool.TestAdapter` |
| pytest | `revitdevtool_pytest` from its own repository | `revitdevtool-pytest` `0.6.1`, built from this repository |

## 1. Remove 3.x

The 4 installer does not remove a 3.x install. Do it first, so the old Daemon and the old bundle do not stay behind.

1. Close Revit, AutoCAD, and every other Autodesk host.
2. Quit the old Daemon from the notification area, if it is running.
3. Open Windows **Settings**, **Apps**, **Installed apps**, find **RevitDevTool**, and choose **Uninstall**.
4. Check that `%APPDATA%\Autodesk\ApplicationPlugins\RevitDevTool.bundle` is gone. Delete the folder if it is still
   there.

## 2. Install 4

1. Download `RevitDevTool-Setup.exe` from the newest `daemon/v...` release on
   [GitHub Releases](https://github.com/trgiangv/RevitDevTool/releases?q=daemon) and run it.
2. Open the Daemon window and go to the **Updates** tab.
3. Tick **Include pre-releases** while 4 is in beta.
4. Tick each host and version you use, then choose **Update**. Each row installs the add-in for that host and version.
5. Start the host. The panel title shows the installed version, for example `DevTools 4.0.0-beta.8`.

See [Installation](/docs/getting-started/installation) for the ribbon buttons and a first script.

## 3. Keep it up to date

- The Daemon and the add-in each follow a channel: `stable`, or `prerelease` when **Include pre-releases** is on.
- **Check for updates** looks for new versions. **Install updates automatically** installs them without asking.
- An add-in update for a host that is running waits until that host closes. The other hosts update right away.
- Restart the host after an add-in update, so it loads the new version.
- Releases of `4.0.0-beta.7` and earlier cannot read the new update channel. If you installed one of them, run
  `RevitDevTool-Setup.exe` once more.

## 4. Point your AI client at the new server

The MCP server is the Daemon itself. Replace the old command in your client's MCP configuration:

```json title="mcp.json"
{
  "mcpServers": {
    "revitdevtool": {
      "type": "stdio",
      "command": "%LOCALAPPDATA%/RevitDevTool/current/DevTools.Daemon.exe",
      "args": ["--stdio"]
    }
  }
}
```

Tools that changed:

| 3.x | 4 |
| --- | --- |
| `list_host_instances` | `list_processes` |
| `open_model` | `open_document`, called inside the host through `code_mode` |
| Searching and calling host tools one by one | `code_mode`: the client writes one short C# program that searches, describes, invokes, and reads host capabilities |

The full list is in [MCP Protocol](/docs/mcp/MCP-Protocol).

## 5. Reload your toolsets

- Load a .NET toolset from the **MCP** toggle in the panel with **Load .NET Toolset**, or a Python toolset folder (a
  `*mcp.py` entry file) with **Load Python Toolset**. An AI client can do the same with `add_tools`.
- A .NET toolset ships its own `ModelContextProtocol` package. Keep it copy-local and do not set
  `ExcludeAssets="runtime"`; the host does not lend its copy. A toolset built without it loads but cannot return
  results.
- Rebuild the toolset against the MCP SDK version in [MCP .NET](/docs/mcp/MCP-CSharp).

## 6. Update your tests

### pytest

- Install `revitdevtool-pytest` `0.6.1` (see [pytest](/docs/testing/pytest)).
- Renamed options:

| Old option | New option |
| --- | --- |
| `--host-launch` | `--force-launch` |
| `--host-launch-timeout` | `--launch-timeout` |
| `--host-timeout` | `--per-test-timeout` |

- The host pipe is now named `DevTools_{Host}_{Version}_{PID}`. Update `--host-pipe` if you pass it.
- IronPython tests: name the file `test_*_ipy.py` and write `unittest` test cases (see [unittest](/docs/testing/unittest)).

### .NET tests (new)

- Add the `RevitDevTool.TestAdapter` package, `0.3.0` or later, and pin NUnit, TUnit, or MSTest yourself. The add-in
  ships no test framework.
- The adapter needs the add-in `4.0.0-beta.8` or later and the .NET 10 runtime on the machine that runs `dotnet test`.
- If a project already uses an adapter from `0.0.1` to `0.2.0` with a 4 beta, move it to `0.3.0` and add the
  framework package it runs.
- Start with [Testing Overview](/docs/testing/Testing-Overview).

## What stays the same

- Scripts keep their entry files and the shared state on `sys.__devtool__`.
- Settings and logs stay under `%APPDATA%\RevitDevTool`.
- Supported hosts: Revit, AutoCAD, Civil 3D, Plant 3D, and the AutoCAD verticals, 2022 to 2027.
