# PythonNetStubGenerator Architecture Documentation

**Internal design documentation for PythonNetStubGenerator utility developers.**

---

## 📚 Documentation Files

### Core Architecture
- **[00-overview.md](00-overview.md)** - System architecture and stub generation pipeline
- **[01-developer-guide.md](01-developer-guide.md)** - Development workflow and contribution guide

---

## 🎯 Quick Navigation

### Understanding the System
Start with [00-overview.md](00-overview.md) to understand:
- Purpose of Python type hints for .NET libraries
- Stub generation pipeline (Reflection → Roslyn → Python syntax)
- XML documentation integration
- Symbol scope management

### Contributing Code
See [01-developer-guide.md](01-developer-guide.md) for:
- Building the dotnet tool
- Testing stub generation
- Adding new type mappings
- Handling complex generic types
- Improving docstring extraction

---

## 🔗 Related Resources

- **Source Code:** [source/PythonNetStubGenerator/](../../../source/PythonNetStubGenerator/)
- **Original Project:** [pythonnet-stub-generator on GitHub](https://github.com/daniil-berg/pythonnet-stub-generator)
- **Usage Documentation:** [CodeExecute-StubGeneration.md (Wiki)](https://github.com/trgiangv/RevitDevTool/wiki/CodeExecute-StubGeneration)

---

## 🏗️ Utility Purpose

PythonNetStubGenerator creates Python type hint files (`.pyi` stubs) for .NET assemblies used via Python.NET. This enables:

1. **IDE IntelliSense** - Autocomplete for .NET types in Python editors
2. **Type Checking** - Mypy/Pyright can validate .NET API usage
3. **Documentation** - Stub docstrings from XML doc comments
4. **Developer Efficiency** - Faster coding with accurate hints

**Example Generated Stub:**

```python
# From Autodesk.Revit.DB.dll
class Wall(HostObject):
    """Represents a wall element in the Revit model."""
    
    def __init__(self) -> None: ...
    
    @property
    def WallType(self) -> WallType:
        """Gets or sets the wall type."""
        ...
    
    def Flip(self) -> None:
        """Flips the wall orientation."""
        ...
```

---

## 🎯 Integration with RevitDevTool

PythonNetStubGenerator is used during RevitDevTool setup:

1. **Revit API assemblies** analyzed (RevitAPI.dll, RevitAPIUI.dll)
2. **Type stubs generated** for 3000+ Revit classes
3. **Stubs distributed** with RevitDevTool Python environment
4. **IDE configured** to recognize stubs location

This provides full IntelliSense for Revit API in Python scripts.

---

_Last updated: 2026-02-14_
