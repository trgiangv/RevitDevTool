# Trace Logging System - Architecture Documentation

**Overview:** RevitDevTool's logging subsystem captures all Trace/Console/Debug output with real-time color-coding, JSON formatting, and file export capabilities.

**Target Audience:** Developers extending logging features; trace listeners and output sinks  
**Time:** 15 min comprehensive read

---

## Quick Reference

| Component | Purpose | Source |
|-----------|---------|--------|
| **LoggingService** | Main orchestrator | [LoggingService.cs](source/RevitDevTool/Logging/LoggingService.cs) |
| **ILogOutputSink** | UI output adapter | [ILogOutputSink.cs](source/RevitDevTool/Logging/ILogOutputSink.cs) |
| **LoggerTraceListener** | Capture .NET Trace events | [LoggerTraceListener.cs](source/RevitDevTool/Logging/Listeners/LoggerTraceListener.cs) |
| **GeometryListener** | Handle geometry visualization | [GeometryListener.cs](source/RevitDevTool/Logging/Listeners/GeometryListener.cs) |
| **ConsoleRedirector** | Intercept Console.WriteLine | [ConsoleRedirector.cs](source/RevitDevTool/Logging/Listeners/ConsoleRedirector.cs) |
| **LogTheme** | Color and style system | [LogTheme.cs](source/RevitDevTool/Logging/Theme/LogTheme.cs) |
| **PyTrace** | Python integration | [PyTrace.cs](source/RevitDevTool/Logging/Python/PyTrace.cs) |

---

## Core Concepts

### Logging Flow

```
┌─ Trace.TraceInformation("message")
│
├─ LoggerTraceListener (captures event)
│
├─ ILoggerAdapter (processes level/theme)
│
├─ ILogOutputSink (renders to UI)
│  └─ RichTextBox or equivalent
│
└─ Optional file export
   └─ Serilog sink
```

### Key Interfaces

**ILoggingService** - Central lifecycle manager
- Initialize() - Setup loggers and listeners
- Restart() - Reset with theme change
- RegisterTraceListeners() - Hook System.Diagnostics
- SetMinimumLevel() - Control verbosity
- ClearOutput() - Reset view

**ILogOutputSink** - UI rendering adapter
- Write(message, style) - Output text with color
- Clear() - Clear all content
- SetTheme() - Apply dark/light mode

**ILoggerAdapter** - Log level detection & formatting
- Log(message, level) - Process and emit message
- Format(object) - Pretty-print JSON objects

---

## Architecture Sections

**[Architecture.md](Architecture.md)** - Complete system design
- Component interactions
- Listener pipeline
- Theme system
- Performance optimization

**[Color-Keywords.md](Color-Keywords.md)** - Keyword detection engine & theming
- Log level detection (ERROR, WARNING, INFO)
- Keyword matching patterns & priority
- Custom keyword configuration
- LogTheme & color presets
- Settings editor integration

**[Python-Stack-Traces.md](Python-Stack-Traces.md)** - Python integration
- PyTrace .NET bridge class
- trace.py Python helper module
- Stack trace capture & formatting
- Integration with CodeExecute
- Usage examples & troubleshooting

---

## Common Tasks

**Extend logging for new event type?**
→ See [Architecture.md](Architecture.md) - Custom listeners

**Add new color theme?**
→ See [Color-Keywords.md](Color-Keywords.md) - LogTheme configuration

**Integrate Python stack traces?**
→ See [Python-Stack-Traces.md](Python-Stack-Traces.md) - PyTrace setup

**Export logs to custom format?**
→ See [Architecture.md](Architecture.md) - ILogOutputSink implementation

---

**Next:** Read [Architecture.md](Architecture.md) for system design details.
