# PythonNetStubGenerator - System Overview

## Purpose

PythonNetStubGenerator is a **dotnet tool** that generates Python type hints (`.pyi` stub files) for .NET assemblies accessed via Python.NET. It enables rich IDE support (IntelliSense, type checking) when writing Python code that calls .NET APIs.

---

## Problem Statement

When using Python.NET to call .NET libraries:

```python
import clr
clr.AddReference("RevitAPI")
from Autodesk.Revit.DB import Wall, FilteredElementCollector

# Without stubs: No autocomplete, no type hints, no docstrings
collector = FilteredElementCollector(doc)  # IDE shows no hints
walls = collector.OfClass(Wall)  # No autocomplete for Wall methods
```

Python IDEs (VS Code, PyCharm) cannot infer types from .NET assemblies loaded at runtime via Python.NET.

**Solution:** Generate `.pyi` stub files that declare .NET types in Python syntax:

```python
# Autodesk/Revit/DB/Wall.pyi (generated stub)
class Wall(HostObject):
    """Represents a wall element."""
    def __init__(self) -> None: ...
    @property
    def WallType(self) -> WallType: ...
```

With stubs, IDEs provide full IntelliSense for .NET APIs in Python code.

---

## Architecture

### High-Level Pipeline

```
┌─────────────────────────────────────────────────────────────┐
│                   Input: .NET DLLs                          │
│            (RevitAPI.dll, RevitAPIUI.dll, etc.)             │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│              Reflection Layer (C#)                          │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Assembly.LoadFrom() → Assembly metadata              │  │
│  │  Extract: Types, Methods, Properties, Constructors    │  │
│  └───────────────────┬───────────────────────────────────┘  │
└────────────────────────┼───────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              Roslyn Semantic Analysis                       │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Parse type signatures with Roslyn                    │  │
│  │  Resolve generic types (IEnumerable<T>, etc.)         │  │
│  │  Map namespaces to Python module structure            │  │
│  └───────────────────┬───────────────────────────────────┘  │
└────────────────────────┼───────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              XML Doc Reader                                 │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Load XML documentation (RevitAPI.xml)                │  │
│  │  Match members via XmlMemberId (M:Type.Method)        │  │
│  │  Extract <summary>, <param>, <returns> tags           │  │
│  └───────────────────┬───────────────────────────────────┘  │
└────────────────────────┼───────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              Type Mapping (C# → Python)                     │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  string → str                                         │  │
│  │  int → int                                            │  │
│  │  IEnumerable<T> → Iterable[T]                         │  │
│  │  Nullable<T> → T | None                               │  │
│  │  Generic constraints → TypeVar bounds                 │  │
│  └───────────────────┬───────────────────────────────────┘  │
└────────────────────────┼───────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              Stub File Writer                               │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  Generate .pyi files (Python stub syntax)             │  │
│  │  Organize by namespace: Autodesk/Revit/DB/__init__.pyi│  │
│  │  Write class definitions, method signatures, props    │  │
│  └───────────────────┬───────────────────────────────────┘  │
└────────────────────────┼───────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│          Output: Python Stub Files (.pyi)                   │
│     typings/Autodesk/Revit/DB/__init__.pyi                  │
│     typings/Autodesk/Revit/UI/__init__.pyi                  │
└─────────────────────────────────────────────────────────────┘
```

---

## Core Components

### 1. StubBuilder (`StubBuilder.cs`)
**Purpose:** Orchestrates the entire stub generation process

**Key Responsibilities:**
- Load target assemblies via Reflection
- Iterate through all public types
- Filter out compiler-generated types
- Coordinate with other components to generate stubs
- Write output files to destination folder

**Key Methods:**
```csharp
public void GenerateStubs(string[] dllPaths, string outputPath)
{
    foreach (var dll in dllPaths)
    {
        var assembly = Assembly.LoadFrom(dll);
        var types = assembly.GetExportedTypes();
        
        foreach (var type in types)
        {
            var stub = GenerateTypeStub(type);
            writer.WriteStub(stub);
        }
    }
}
```

### 2. PythonTypes (`PythonTypes.cs`)
**Purpose:** Map .NET types to Python type hint syntax

**Key Mappings:**

| .NET Type | Python Type |
|-----------|-------------|
| `string` | `str` |
| `int`, `Int32` | `int` |
| `double`, `Double` | `float` |
| `bool` | `bool` |
| `void` | `None` |
| `IEnumerable<T>` | `Iterable[T]` |
| `IList<T>` | `List[T]` |
| `Dictionary<K,V>` | `Dict[K, V]` |
| `Nullable<T>` | `T \| None` |
| `T[]` | `List[T]` |

**Generic Type Handling:**
```csharp
// Input: IEnumerable<Wall>
// Output: Iterable[Wall]

public string MapType(Type type)
{
    if (type.IsGenericType)
    {
        var genericDef = type.GetGenericTypeDefinition();
        var typeArgs = type.GetGenericArguments();
        
        if (genericDef == typeof(IEnumerable<>))
        {
            return $"Iterable[{MapType(typeArgs[0])}]";
        }
    }
    
    return type.Name;  // Fallback
}
```

### 3. XmlDocReader (`XmlDocReader.cs`)
**Purpose:** Extract documentation from XML doc files

**Key Features:**
- Load XML documentation files (e.g., `RevitAPI.xml`)
- Match XML member IDs to Reflection members
- Parse `<summary>`, `<param>`, `<returns>`, `<exception>` tags
- Generate Python docstrings in Google format

**XML Member ID Format:**
```xml
<!-- RevitAPI.xml -->
<member name="M:Autodesk.Revit.DB.Wall.Flip">
  <summary>Flips the orientation of the wall.</summary>
</member>

<member name="P:Autodesk.Revit.DB.Wall.WallType">
  <summary>Gets or sets the wall type.</summary>
</member>
```

**Generated Docstring:**
```python
def Flip(self) -> None:
    """Flips the orientation of the wall."""
    ...

@property
def WallType(self) -> WallType:
    """Gets or sets the wall type."""
    ...
```

### 4. XmlMemberIdBuilder (`XmlMemberIdBuilder.cs`)
**Purpose:** Generate XML documentation member IDs from Reflection metadata

**Member ID Format (from ECMA-334 spec):**
- `T:Namespace.TypeName` - Type
- `M:Namespace.TypeName.MethodName` - Method
- `P:Namespace.TypeName.PropertyName` - Property
- `F:Namespace.TypeName.FieldName` - Field
- `E:Namespace.TypeName.EventName` - Event

**Example:**
```csharp
// Input: MethodInfo for Wall.Flip()
// Output: "M:Autodesk.Revit.DB.Wall.Flip"

public string GetMemberId(MethodInfo method)
{
    return $"M:{method.DeclaringType.FullName}.{method.Name}";
}
```

### 5. SymbolScope (`SymbolScope.cs`)
**Purpose:** Manage symbol namespaces and imports

**Key Responsibilities:**
- Track which types are defined in current module
- Track which types need to be imported
- Generate `from X import Y` statements
- Handle circular dependencies (use string literals for forward refs)

**Example:**
```python
# Generated stub with proper imports
from typing import List, Iterable
from Autodesk.Revit.DB import Element, ElementId

class Wall(Element):
    def GetDependentElements(self, filter: "ElementFilter") -> List[ElementId]:
        ...  # "ElementFilter" as string to avoid circular import
```

### 6. StubWriter (`StubWriter.cs`)
**Purpose:** Write formatted `.pyi` stub files

**Key Features:**
- Generate directory structure matching namespaces
- Write class definitions with proper indentation
- Add `__all__` export lists
- Format method signatures with proper line wrapping

**Output Example:**
```python
# Autodesk/Revit/DB/__init__.pyi

from typing import List, Iterable, overload
from Autodesk.Revit.DB import Element, ElementId

__all__ = ['Wall', 'WallType', 'FilteredElementCollector']

class Wall(Element):
    """Represents a wall element in the Revit model."""
    
    def __init__(self) -> None: ...
    
    @property
    def WallType(self) -> WallType:
        """Gets or sets the wall type."""
        ...
    
    @WallType.setter
    def WallType(self, value: WallType) -> None: ...
    
    def Flip(self) -> None:
        """Flips the wall orientation."""
        ...
```

---

## Key Design Decisions

### Why Roslyn instead of pure Reflection?
- **Generic type resolution:** Roslyn provides semantic analysis for complex generics
- **Type constraints:** Roslyn can extract `where T : struct` constraints
- **Future extensibility:** Roslyn enables advanced C# syntax analysis

### Why XML docs instead of Reflection alone?
- **Reflection only provides signatures**, not documentation
- **XML docs contain human-readable descriptions**
- **Standard format** generated by C# compiler (`/doc` flag)

### Output Structure: Flat vs Nested

**Choice: Nested modules matching .NET namespaces**

```
typings/
├── Autodesk/
│   └── Revit/
│       ├── DB/
│       │   └── __init__.pyi
│       └── UI/
│           └── __init__.pyi
```

**Benefits:**
- Matches Python import syntax: `from Autodesk.Revit.DB import Wall`
- Gradual loading (import only needed modules)
- Clear namespace organization

**Tradeoff:** More files generated (1 per namespace vs 1 per assembly)

---

## Performance Characteristics

### Generation Speed
- **Small assembly (100 types):** < 1 second
- **Revit API (3000+ types):** 5-10 seconds
- **Large framework (10,000+ types):** 30-60 seconds

**Bottlenecks:**
1. **Reflection loading:** Assembly.LoadFrom() is slow for large DLLs
2. **XML doc parsing:** XPath queries for 10,000+ members
3. **File I/O:** Writing 1000+ stub files

**Optimization Strategies:**
- Parallel processing of types (future enhancement)
- XML doc caching in memory
- Batch write operations

### Output Size
- **Revit API stubs:** ~15 MB, 3000+ classes
- **.NET Core BCL stubs:** ~50 MB, 10,000+ types

---

## Limitations and Known Issues

### Limitation: No Runtime Information
Stubs are generated from **static metadata only**. Cannot capture:
- Dynamic dispatch (Python `__getattr__`)
- Runtime-added properties
- Python.NET operator overloads (may map incorrectly)

### Limitation: Generic Constraints
.NET generic constraints don't map perfectly to Python TypeVars:

```csharp
// C#: Generic constraint
public T Create<T>() where T : Element, new()
{
    return new T();
}
```

```python
# Python stub: Limited constraint expression
T = TypeVar('T', bound=Element)
def Create(self) -> T: ...
```

Python TypeVars support `bound` but not multiple constraints like `new()`.

### Known Issue: Overload Resolution
Python `@overload` doesn't perfectly match C# overloading. Multiple C# overloads may collapse into one Python signature with `Any` types.

**Example:**
```csharp
// C#: Two overloads
public void SetParameter(string name, int value) { }
public void SetParameter(string name, string value) { }
```

```python
# Python stub: Ideally separate overloads
@overload
def SetParameter(self, name: str, value: int) -> None: ...
@overload
def SetParameter(self, name: str, value: str) -> None: ...

# But may generate: (less precise)
def SetParameter(self, name: str, value: Any) -> None: ...
```

### Known Issue: Event Handling
.NET events don't map cleanly to Python properties. Currently not stubbed.

---

## File Structure

```
source/PythonNetStubGenerator/
├── PythonNetStubGenerator.csproj   # Project file
├── README.md                        # User-facing usage guide
│
├── StubBuilder.cs                   # Main orchestrator
├── StubWriter.cs                    # File output
│
├── PythonTypes.cs                   # Type mapping (C# → Python)
├── MethodComparer.cs                # Deduplication utility
│
├── XmlDocReader.cs                  # XML doc file reader
├── XmlDocProvider.cs                # Doc lookup interface
├── XmlMemberIdBuilder.cs            # Generate XML member IDs
│
├── SymbolScope.cs                   # Namespace/import tracking
├── ClassScope.cs                    # Class-level symbol scope
├── DocComment.cs                    # Docstring formatting
│
└── StringBuilderExtensions.cs       # Utility methods
```

---

## Dependencies

### NuGet Packages
- **Microsoft.CodeAnalysis.CSharp** (Roslyn) - Semantic analysis of C# types
- **System.Reflection.Metadata** - Assembly loading and inspection

### Runtime Requirements
- **.NET 6.0+** SDK
- Target assemblies must be .NET Standard 2.0+ or .NET Framework 4.7.2+

---

## Usage in RevitDevTool

### 1. Stub Generation During Setup

```powershell
# Install tool globally
dotnet tool install --global pythonnetstubgenerator.tool

# Generate stubs for Revit API
GeneratePythonNetStubs `
  --dest-path="../RevitDevTool.PythonDemo/.venv/typings" `
  --target-dlls="C:/Program Files/Autodesk/Revit 2024/RevitAPI.dll;C:/Program Files/Autodesk/Revit 2024/RevitAPIUI.dll"
```

### 2. IDE Configuration

**VS Code (`.vscode/settings.json`):**
```json
{
  "python.analysis.extraPaths": [
    "${workspaceFolder}/source/RevitDevTool.PythonDemo/.venv/typings"
  ]
}
```

**PyCharm:**
Settings → Project → Python Interpreter → Show All → Show Paths → Add `.venv/typings`

### 3. Result: Full IntelliSense

```python
from Autodesk.Revit.DB import Wall, FilteredElementCollector

collector = FilteredElementCollector(doc)
walls = collector.OfClass(Wall)  # IDE shows autocomplete for Wall methods

for wall in walls:
    wall_type = wall.WallType  # Property type hint: WallType
    wall.Flip()                # Method signature shown in IDE
```

---

## Related Documentation

- **[01-developer-guide.md](01-developer-guide.md)** - Development and contribution guide
- **[CodeExecute-StubGeneration.md (Wiki)](https://github.com/trgiangv/RevitDevTool/wiki/CodeExecute-StubGeneration)** - User guide for stub generation

---

_Last updated: 2026-02-14_
