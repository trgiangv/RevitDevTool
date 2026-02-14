# Logging Architecture

Technical documentation for the unified logging system.

---

## 📚 Documentation

### [00-overview.md](00-overview.md)
Quick reference with component table and responsibilities

### [01-developer-guide.md](01-developer-guide.md)
Complete architecture guide:
- System initialization and lifecycle
- Listener pipeline design
- Output sink architecture
- Thread safety and performance

### [03-Theme-System.md](03-Theme-System.md)
Color scheme and styling:
- Keyword detection patterns
- Theme customization
- Style application logic

### [05-Python-Integration.md](05-Python-Integration.md)
Python.NET bridge:
- `print()` redirection via PyTrace
- Stack trace capture and formatting
- Python-C# interop patterns

---

## 🎯 Quick Navigation

### For Users
- **[Logging-Overview (Wiki)](https://github.com/trgiangv/RevitDevTool/wiki/Logging-Overview)** - User guide and formatting
- **[Color Keywords (Wiki)](https://github.com/trgiangv/RevitDevTool/wiki/Logging-ColorKeywords)** - Syntax highlighting reference
- **[Python Stack Traces (Wiki)](https://github.com/trgiangv/RevitDevTool/wiki/Logging-PythonStackTraces)** - Python debugging

### For Developers
- **[System Overview](00-overview.md)** - Component reference
- **[Developer Guide](01-developer-guide.md)** - Complete architecture
- **[Theme System](03-Theme-System.md)** - Color customization
- **[Python Support](05-Python-Integration.md)** - Python integration
- **[Source Code](../../../source/RevitDevTool/Logging/)** - Implementation

### Key Components
Located in `source/RevitDevTool/Logging/`:
- **LoggingService.cs** - Main orchestrator
- **Listeners/** - Trace listeners (LoggerTrace, GeometryListener, NotifyListener)
- **Serilog/** - Log formatting and enrichment
- **Theme/** - Color schemes and keyword detection
- **Python/** - PyTrace bridge for Python integration

---

## 🏗️ Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                   User Code                             │
│  Trace, Console, Python print(), Debug                 │
└──────────────────┬──────────────────────────────────────┘
                   │
         ┌─────────▼─────────┐
         │  TraceListeners   │
         │  (Capture Layer)  │
         └─────────┬─────────┘
                   │
     ┌─────────────┼─────────────┐
     │             │             │
 ┌───▼──────┐ ┌───▼──────┐ ┌────▼──────┐
 │LoggerTrace│ │Geometry  │ │Notify     │
 │Listener   │ │Listener  │ │Listener   │
 └───┬───────┘ └───┬──────┘ └────┬──────┘
     │             │              │
     │ (Format)    │ (Route to    │ (Events)
     │             │  Viz)        │
 ┌───▼──────────┐  │              │
 │ Serilog      │  │              │
 │ Pipeline     │  │              │
 │ - Enrichers  │  │              │
 │ - Formatters │  │              │
 │ - Theme      │  │              │
 └───┬──────────┘  │              │
     │             │              │
 ┌───▼─────────────▼──────────────▼─┐
 │       ILogOutputSink              │
 │  (RichTextBox, File, Remote)     │
 └───────────────────────────────────┘
```

---

## 🔑 Key Concepts

### Listener Pipeline
Multiple listeners process each trace event in parallel:
- **LoggerTraceListener:** Format and route to output sinks
- **GeometryListener:** Intercept geometry objects, send to visualization
- **NotifyListener:** Broadcast events for UI updates

### Geometry Interception
`GeometryListener` checks if traced value is Revit geometry type. If yes:
- Route to Visualization system instead of log output
- Prevents geometry objects from appearing in text logs

### Theme System
- **LogTheme:** Color scheme loaded from JSON
- **LogStyle:** Per-level formatting (INFO, WARN, ERROR)
- **Keyword Detection:** Regex patterns for syntax highlighting
- **Dynamic Application:** Applied during formatting pipeline

### Output Sinks
`ILogOutputSink` abstraction enables multiple destinations:
- **RichTextBox:** UI display with colors
- **File:** Persistent logging
- **Remote:** Network logging (future)

### Serilog Integration
- Structured logging with enrichers
- Property injection (Revit context, timestamps)
- Flexible formatting with message templates
- Performance-optimized for high-throughput scenarios

---

## 📖 Related Documentation

- **[Visualization Architecture](../../Visualization/architecture/)** - Geometry rendering integration
- **[CodeExecute Architecture](../../CodeExecute/architecture/)** - Script execution integration

---

_Last updated: 2026-02-14_
