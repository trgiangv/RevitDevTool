# RevitDevTool - Architecture Documentation

This folder contains **internal architecture documentation** for developers contributing to RevitDevTool.

**For user guides and tutorials, see:** [RevitDevTool.Wiki](https://github.com/trgiangv/RevitDevTool/wiki)

---

## 📁 Structure

```
docs/
├── CodeExecute/
│   └── architecture/          # CodeExecute internal design
├── Logging/
│   └── architecture/          # Logging internal design
├── Visualization/
│   └── architecture/          # Visualization internal design
├── PythonDemo/
│   └── architecture/          # PythonDemo internal design
└── PythonNetStubGenerator/
    └── architecture/          # Stub generator internal design
```

---

## 📚 Architecture Documentation

### CodeExecute Module
**Path:** `CodeExecute/architecture/`

Internal design documentation:
- System architecture and design patterns
- Provider pattern implementation
- Strategy pattern for execution
- Tree model structure
- Python implementation details
- Dependency resolution internals

### Logging Module
**Path:** `Logging/architecture/`

Internal design documentation:
- System overview and architecture
- Developer guide for extensions
- Serilog sink implementation
- Theme system design
- Python integration bridge

### Visualization Module
**Path:** `Visualization/architecture/`

Internal design documentation:
- System overview and architecture
- Developer guide for extensions
- DirectContext3D implementation
- Geometry type handlers
- Buffer management strategy

### PythonDemo Module
**Path:** `PythonDemo/architecture/`

Internal design documentation:
- Python + WebView2 dashboard architecture
- React frontend integration patterns
- Polars analytics engine design
- Data collection and export workflow
- Quality gates and development workflow

### PythonNetStubGenerator Utility
**Path:** `PythonNetStubGenerator/architecture/`

Internal design documentation:
- Type stub generation pipeline
- .NET to Python type mapping
- XML documentation integration
- Roslyn semantic analysis
- Symbol scope management

---

## 🎯 For Different Audiences

### I want to **use** RevitDevTool
👉 See: [RevitDevTool.Wiki](https://github.com/trgiangv/RevitDevTool/wiki)
- User guides and tutorials
- API usage examples
- Comparison with other tools
- Quick start guides

### I want to **contribute** to RevitDevTool
👉 Read this folder's architecture docs
- Understand internal design patterns
- Learn system architecture
- Extend modules with custom providers/handlers
- Debug and troubleshoot

### I want to understand **how it works**
👉 Start with architecture overviews:
- [CodeExecute/architecture/10-system-design.md](CodeExecute/architecture/10-system-design.md)
- [Logging/architecture/00-overview.md](Logging/architecture/00-overview.md)
- [Visualization/architecture/00-overview.md](Visualization/architecture/00-overview.md)
- [PythonDemo/architecture/00-overview.md](PythonDemo/architecture/00-overview.md)
- [PythonNetStubGenerator/architecture/00-overview.md](PythonNetStubGenerator/architecture/00-overview.md)

---

## 📊 Documentation Completeness

| Module | Architecture Docs | Source README | Status |
|--------|-------------------|---------------|--------|
| **CodeExecute** | ✅ Complete | ✅ Yes | Production |
| **Logging** | ✅ Complete | ✅ Yes | Production |
| **Visualization** | ✅ Complete | ✅ Yes | Production |
| **PythonDemo** | ✅ Complete | ✅ Yes | Production |
| **PythonNetStubGenerator** | ✅ Complete | ✅ Yes | Utility |
| **DotnetDemo** | ⚠️ Source README only | ✅ Yes | Examples |

**Legend:**
- ✅ Complete: Full architecture documentation with overview and developer guide
- ⚠️ Source README only: Basic documentation in source folder, no architecture docs yet
- Production: Core functionality module
- Utility: Supporting tool for development
- Examples: Demo commands for testing and reference

---

## 🔗 Related Documentation

- **User Documentation:** [RevitDevTool.Wiki](https://github.com/trgiangv/RevitDevTool/wiki)
- **Source Code:** [../source/](../source/)
- **Demo Commands (.NET):** [../source/RevitDevTool.DotnetDemo/](../source/RevitDevTool.DotnetDemo/)
- **Demo Scripts (Python):** [../source/RevitDevTool.PythonDemo/commands/](../source/RevitDevTool.PythonDemo/commands/)

---

## 📝 Contributing

When contributing to RevitDevTool:

1. **Read architecture docs** for the module you're modifying
2. **Follow existing patterns** (Provider, Strategy, Tree Model)
3. **Update architecture docs** if you change internal design
4. **Add demo commands** in RevitDevTool.DotnetDemo/ or scripts in RevitDevTool.PythonDemo/
5. **Update user docs** in RevitDevTool.Wiki if user-facing changes

---

## 🏗️ Design Philosophy

RevitDevTool follows these principles:

1. **Separation of Concerns** - Each module has clear responsibilities
2. **Extensibility** - Provider/Strategy patterns for pluggable behavior
3. **Performance** - Buffering, caching, async where appropriate
4. **Type Safety** - Strong typing with nullability annotations
5. **Testability** - Dependency injection and mockable interfaces

---

_Last updated: 2026-02-14_
