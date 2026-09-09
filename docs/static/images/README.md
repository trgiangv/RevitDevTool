# Documentation images

Images imported from `RevitDevTool.Wiki/images/` retain their source grouping:

- `start/` — installation and first-launch UI
- `execution/` — Python, C#, F#, debugging, and dependency workflows
- `testing/` — host testing and pytest bridge
- `mcp/` — MCP client and SDK workflows
- `observability/` — logging, tracing, and crash diagnostics
- `examples/` — dashboard and sample workflows

Use root-relative paths in Docusaurus Markdown, for example:

```md
![Python debugger](/images/execution/PythonDebugger.gif)
```

New image categories should be created here before adding references from a
public documentation page.
