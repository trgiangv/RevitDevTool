---
sidebar_position: 1
slug: /
sidebar_label: Overview
---

# Overview

RevitDevTool brings standard development ecosystems into live Autodesk CAD/BIM hosts without replacing them.

Use the languages, package managers, test frameworks, and AI protocols you already know:

```mermaid
flowchart TD
    Dev[Your code<br/>C# · F# · Python · IronPython]
    Tools[Your tools<br/>NuGet · PyPI · NUnit · TUnit · pytest · MCP SDK]
    Runtime[RevitDevTool runtime]
    Host[Live Revit or AutoCAD host]

    Dev --> Tools --> Runtime --> Host
```

It is a developer platform for execution, debugging, testing, visualization, and host integration across Revit and AutoCAD-family applications.

![RevitDevTool ribbon and dockable panel](/images/start/Ribbon.png)

## Choose a path

- [Install RevitDevTool](getting-started/installation)
- [Resolve Python dependencies inline](execution/Python-Dependencies)
- [Run tests inside a live host](testing/Testing-Overview)
- [Explore MEP-oriented examples](https://github.com/trgiangv/RevitDevTool/tree/develop/samples)
