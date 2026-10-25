---
sidebar_position: 2
---

# Installation

RevitDevTool installs as an add-in bundle for Revit and AutoCAD-based products. One installer covers every supported host and year.

:::info Prerequisites

- Windows 10 or 11
- Autodesk Revit, AutoCAD, Civil 3D or Plant 3D, 2022–2027
- [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0), only if you connect an AI client through MCP
  :::

## Install

1. Download `RevitDevTool-Setup.exe` from [GitHub Releases](https://github.com/trgiangv/RevitDevTool/releases/latest).
2. Run the installer.
3. Start your Autodesk application.
4. Open the RevitDevTool (Revit) or DevTools (AutoCAD) panel from the ribbon.

import Tabs from '@theme/Tabs';
import TabItem from '@theme/TabItem';

<Tabs groupId="host" defaultValue="revit">
<TabItem value="revit" label="Revit">

On the **Add-Ins** tab, under **External Tools**, choose **DevTools**. **StubBuilder** and **Commands** sit beside it.

![RevitDevTool buttons on the Revit Add-Ins ribbon](/images/start/Revit_Ribbon.png)

</TabItem>
<TabItem value="autocad" label="Civil 3D and AutoCAD">

On the **Add-ins** tab, under **External Tools**, choose **DevTools**. **StubBuilder** sits beside it.

![DevTools buttons on the Civil 3D Add-ins ribbon](/images/start/Civil3D_Ribbon.png)

</TabItem>
</Tabs>

That's it. The installer registers the add-in bundle under `%APPDATA%\Autodesk\ApplicationPlugins\RevitDevTool.bundle\`.

:::tip No administrator rights needed
The installer works for the current Windows user only, so it runs without elevation.
:::

## Run your first script

Save this file as `hello_script.py` in an empty folder:

```python title="hello_script.py"
# /// script
# dependencies = ["polars"]
# ///

import polars as pl
from Autodesk.Revit import DB

doc = __revit__.ActiveUIDocument.Document
walls = DB.FilteredElementCollector(doc).OfClass(DB.Wall)

print(pl.DataFrame({"name": [w.Name for w in walls]}))
```

:::tip Dependencies live in the script
The `# /// script` header lists the packages the script needs. They install automatically before the first run, so there is no `requirements.txt` to maintain.
:::

Then:

1. Open the RevitDevTool panel and turn on the **Execution** toggle.
2. Choose **Load Scripts**, then select the folder.
3. Pick the discovered command and run it.

The output appears in the panel's log view. On Revit, traced geometry is also drawn in the active view.

A folder shows up as a command only if it holds an entry file: `*script.py` (CPython), `*_ipy_script.py` (IronPython), `*script.csx` (C#) or `*script.fsx` (F#). See [Execution Overview](/docs/execution/Execution-Overview) for what each runtime supports.

Where to go next:

- [Attach a debugger](/docs/execution/python/Execution-PythonDebugging)
- [Python dependencies](/docs/execution/Python-Dependencies)
- [Log output](/docs/logging/Observability-Http)
- [Troubleshooting](/docs/getting-started/Troubleshooting) if something does not work

## Connect an AI client

Point any MCP v2 client that supports stdio (Cursor, Claude, GitHub Copilot, …) at the MCP server in the bundle:

```json title="mcp.json"
{
  "mcpServers": {
    "revitdevtool": {
      "type": "stdio",
      "command": "%APPDATA%/Autodesk/ApplicationPlugins/RevitDevTool.bundle/Contents/DevTools.Daemon.exe",
      "args": ["--stdio"]
    }
  }
}
```

The server finds running hosts automatically. The tools your client gets are listed in [MCP Protocol](/docs/mcp/MCP-Protocol).

## Build from source

Contributors can build from source with the SDK pinned in `global.json`. See [Build From Source](/docs/getting-started/Build-From-Source).
