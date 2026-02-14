# 13: Tree Model - Hierarchical Script/Command Organization

> **Status:** ✅ Implemented (Revit 2022-2026)  
> **Last Updated:** 2026-02-14

**Objective:** Understand how commands and scripts are organized in a hierarchical tree structure  
**Prerequisites:** Read [10-system-design.md](10-system-design.md), [11-provider-pattern.md](11-provider-pattern.md)  
**Time:** 8 min read

---

## Overview

The Tree Model provides a unified hierarchical structure for organizing both .NET assemblies/commands and Python scripts/folders. It implements the **Composite Pattern** to treat individual commands and containers uniformly.

**Key Concepts:**
- **RootNode**: Represents a .NET assembly (.dll) or Python root folder
- **IntermediateNode**: Represents a namespace or subfolder
- **ExecuteNode**: Represents an executable command (IExternalCommand) or script (.py)
- **BaseNode**: Abstract base class implementing common tree functionality

The tree enables:
- Unified UI display for .NET and Python content
- Hierarchical organization (folders/namespaces)
- Search and filtering
- Execution tracking
- State persistence across sessions

---

## Node Hierarchy (Actual Implementation)

```
BaseNode (Abstract)
├─ RootNode
│  ├─ Represents: Assembly (.dll) or Root Folder
│  ├─ Properties: RootPath, ProviderType (DotNet/Python)
│  └─ Contains: IntermediateNode or ExecuteNode children
│
├─ IntermediateNode
│  ├─ Represents: Namespace or SubFolder
│  ├─ Properties: FullPath
│  └─ Contains: IntermediateNode or ExecuteNode children
│
└─ ExecuteNode
   ├─ Represents: IExternalCommand or Python Script
   ├─ Properties: ExecutablePath, ProviderType, SourceFilePath
   ├─ Has: ExecutionStrategy (linked to actual execution logic)
   └─ No children (leaf node)
```

**Example Tree Structure:**

```
[Root] RevitDevTool.DotnetDemo.dll (DotNet)
  ├─ [Intermediate] RevitDevTool.DotnetDemo
  │   ├─ [Execute] CurveVisualization
  │   ├─ [Execute] FaceVisualization
  │   └─ [Execute] Log
  
[Root] PythonScripts/ (Python)
  ├─ [Intermediate] Visualization/
  │   ├─ [Execute] visualization_curve_script.py
  │   └─ [Execute] visualization_xyz_script.py
  └─ [Execute] dashboard_script.py
```

---

## BaseNode - Abstract Base Class

**Location:** [source/RevitDevTool/CodeExecute/Models/BaseNode.cs](../../../source/RevitDevTool/CodeExecute/Models/BaseNode.cs)

All nodes inherit from `BaseNode`, which provides common tree functionality:

```csharp
public abstract partial class BaseNode : ObservableObject
{
    /// <summary>
    /// Unique path-based identifier (survives object recreation)
    /// Examples:
    /// - DotNet: "dotnet://C:/Plugins/Tools.dll|MyCompany.Commands.PurgeCommand"
    /// - Python: "python://View/Cleanup//HideUnused.py"
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display name (file name, class name, etc.)
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Child nodes (Composite Pattern)
    /// </summary>
    public ObservableCollection<BaseNode> Children { get; } = [];

    /// <summary>
    /// UI state - whether node is expanded in tree view
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>
    /// UI state - whether node is selected
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// UI state - whether node is visible (for search filtering)
    /// </summary>
    [ObservableProperty]
    private bool _isVisible = true;

    /// <summary>
    /// Search highlighting range
    /// </summary>
    [ObservableProperty]
    private ISelectionRange? _highlightRange;

    /// <summary>
    /// Execution tracking - is this the last executed item?
    /// </summary>
    [ObservableProperty]
    private bool _isLastExecuted;

    /// <summary>
    /// Execution tracking - when was this last executed?
    /// </summary>
    [ObservableProperty]
    private DateTime? _lastExecutedTime;

    /// <summary>
    /// Execution strategy (null for container nodes)
    /// Linked to actual provider implementation
    /// </summary>
    public IExecutionStrategy? ExecutionStrategy { get; init; }

    /// <summary>
    /// Type of node (Container or Executable)
    /// </summary>
    public required NodeType NodeType { get; init; }

    /// <summary>
    /// Whether this node can be executed
    /// </summary>
    public bool IsExecutable => NodeType == NodeType.Executable && ExecutionStrategy != null;

    /// <summary>
    /// Execute this node if executable
    /// </summary>
    public void Execute()
    {
        if (!IsExecutable) return;
        
        try
        {
            ExecutionStrategy!.Execute();
            IsLastExecuted = true;
            LastExecutedTime = DateTime.Now;
        }
        catch
        {
            IsLastExecuted = false;
            LastExecutedTime = DateTime.Now;
            throw;
        }
    }
}
```

**Key Design Decisions:**
- **Immutable Core Properties**: `Id`, `Name`, `NodeType`, `ExecutionStrategy` are init-only (set during construction)
- **Observable Properties**: UI state (IsExpanded, IsSelected, IsVisible) uses `ObservableObject` for WPF data binding
- **Composite Pattern**: All nodes have `Children` collection, even if empty (simplifies tree operations)
- **Strategy Pattern**: `ExecutionStrategy` links to provider-specific execution logic (see [11-provider-pattern.md](11-provider-pattern.md))

---

## Node Types (Actual Implementation)

**Location:** [source/RevitDevTool/CodeExecute/Models/Nodes.cs](../../../source/RevitDevTool/CodeExecute/Models/Nodes.cs)

### 1. RootNode - Assembly or Folder Root

```csharp
public sealed class RootNode : BaseNode
{
    /// <summary>
    /// Path to the root (assembly file or folder)
    /// </summary>
    public required string RootPath { get; init; }

    /// <summary>
    /// Provider type (DotNet or Python)
    /// </summary>
    public required ExecutionMode ProviderType { get; init; }
}
```

**Usage:**
- **DotNet**: Represents a loaded assembly (.dll file)
  - `RootPath`: `C:\Plugins\MyTools.dll`
  - `Name`: `MyTools.dll`
  - Children: IntermediateNode (namespaces) or ExecuteNode (commands)
  
- **Python**: Represents a root folder
  - `RootPath`: `C:\Scripts\MyScripts`
  - `Name`: `MyScripts`
  - Children: IntermediateNode (subfolders) or ExecuteNode (.py files)

### 2. IntermediateNode - Namespace or SubFolder

```csharp
public sealed class IntermediateNode : BaseNode
{
    /// <summary>
    /// Full path to namespace or folder
    /// </summary>
    public required string FullPath { get; init; }
}
```

**Usage:**
- **DotNet**: Represents a namespace
  - `FullPath`: `MyCompany.RevitTools.Commands`
  - `Name`: `Commands` (last segment)
  - Children: More IntermediateNode (sub-namespaces) or ExecuteNode (commands)
  
- **Python**: Represents a subfolder
  - `FullPath`: `C:\Scripts\MyScripts\Visualization`
  - `Name`: `Visualization` (folder name)
  - Children: More IntermediateNode or ExecuteNode (.py files)

### 3. ExecuteNode - Executable Command or Script

```csharp
public sealed class ExecuteNode : BaseNode
{
    /// <summary>
    /// Full path to the executable (class name or script path)
    /// </summary>
    public required string ExecutablePath { get; init; }

    /// <summary>
    /// Provider type (DotNet or Python)
    /// </summary>
    public required ExecutionMode ProviderType { get; init; }

    /// <summary>
    /// Source file path (for "Open Location" feature)
    /// </summary>
    public string? SourceFilePath { get; init; }
}
```

**Usage:**
- **DotNet**: Represents an `IExternalCommand` implementation
  - `ExecutablePath`: `MyCompany.RevitTools.Commands.PurgeUnusedCommand`
  - `Name`: `PurgeUnusedCommand` (class name)
  - `SourceFilePath`: `C:\Source\MyTools\PurgeUnusedCommand.cs` (if available)
  - `ExecutionStrategy`: `DotNetExecutionStrategy` with reflected method info
  
- **Python**: Represents a Python script (.py file)
  - `ExecutablePath`: `C:\Scripts\MyScripts\cleanup.py`
  - `Name`: `cleanup.py`
  - `SourceFilePath`: Same as ExecutablePath
  - `ExecutionStrategy`: `PythonExecutionStrategy` with PEP 723 metadata

**Important:** ExecuteNode has NO children (leaf node only). The tree structure is:
```
RootNode → IntermediateNode* → ExecuteNode
```

---

## Node Creation and Population

Nodes are created by providers during discovery/scanning:

**DotNet Provider** ([`DotNetExecutionProvider.cs`](../../../source/RevitDevTool/CodeExecute/Providers/DotNet/DotNetExecutionProvider.cs)):
1. Scans assembly for `IExternalCommand` implementations using reflection
2. Creates `RootNode` for the assembly
3. Groups commands by namespace into `IntermediateNode` hierarchy
4. Creates `ExecuteNode` for each command with `DotNetExecutionStrategy`

**Python Provider** ([`PythonExecutionProvider.cs`](../../../source/RevitDevTool/CodeExecute/Providers/Python/PythonExecutionProvider.cs)):
1. Scans root folder recursively for `.py` files
2. Creates `RootNode` for the root folder
3. Creates `IntermediateNode` for each subfolder
4. Creates `ExecuteNode` for each script with `PythonExecutionStrategy`
5. Parses PEP 723 metadata for dependency information

**Tree Building Process:**
```csharp
// Pseudocode
public List<BaseNode> BuildTree(string rootPath)
{
    var rootNode = new RootNode { RootPath = rootPath, ... };
    
    // Scan for executables
    var executables = DiscoverExecutables(rootPath);
    
    // Group by namespace/folder
    var grouped = GroupByHierarchy(executables);
    
    // Build intermediate nodes
    foreach (var group in grouped)
    {
        var intermediate = new IntermediateNode { FullPath = group.Key, ... };
        foreach (var exe in group.Items)
        {
            var executeNode = new ExecuteNode { 
                ExecutablePath = exe.Path,
                ExecutionStrategy = CreateStrategy(exe),
                ...
            };
            intermediate.Children.Add(executeNode);
        }
        rootNode.Children.Add(intermediate);
    }
    
    return [rootNode];
}
```

---

## Future Enhancements (Not Yet Implemented)

The following node types are **planned for future versions** to support workflow-graph functionality:

- **DataNode**: Represents input/output data with type information
- **TransformNode**: Represents data transformations (map, filter, aggregate)
- **ConditionalNode**: If/Then/Else branching logic
- **LoopNode**: Iteration over collections

These would enable visual programming workflows where users can build execution graphs by connecting nodes. Current implementation (v1.x) focuses on hierarchical script/command organization only.

---

## State Persistence

The tree structure is persisted across Revit sessions using `TreeStateManager`:

**Location:** [source/RevitDevTool/CodeExecute/TreeStateManager.cs](../../../source/RevitDevTool/CodeExecute/TreeStateManager.cs)

**Persisted State:**
- Which nodes are expanded/collapsed
- Last executed node (for "Run Last" feature)
- Last execution time per node
- Search filter state

**Storage:**
- JSON file in user's AppData folder
- Keyed by node `Id` (path-based, stable across sessions)
- Loaded on plugin startup
- Saved on plugin shutdown or state changes

**Example State JSON:**
```json
{
  "expandedNodes": [
    "dotnet://C:/Plugins/Tools.dll",
    "dotnet://C:/Plugins/Tools.dll|MyCompany.Commands"
  ],
  "lastExecuted": "python://Scripts//cleanup.py",
  "lastExecutedTime": "2026-02-14T10:30:00Z",
  "nodeExecutionTimes": {
    "python://Scripts//cleanup.py": "2026-02-14T10:30:00Z",
    "dotnet://C:/Plugins/Tools.dll|MyCompany.Commands.PurgeCommand": "2026-02-13T15:20:00Z"
  }
}
```

---

## Tree Operations

Common tree operations implemented in `TreeNodeOperations` service:

**Search/Filter:**
```csharp
// Find nodes matching search text
public List<BaseNode> SearchNodes(BaseNode root, string searchText)
{
    var results = new List<BaseNode>();
    TraverseTree(root, node => {
        if (node.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
        {
            node.IsVisible = true;
            results.Add(node);
        }
        else
        {
            node.IsVisible = false;
        }
    });
    return results;
}
```

**Expand/Collapse All:**
```csharp
public void ExpandAll(BaseNode root)
{
    TraverseTree(root, node => node.IsExpanded = true);
}

public void CollapseAll(BaseNode root)
{
    TraverseTree(root, node => node.IsExpanded = false);
}
```

**Find Node by Id:**
```csharp
public BaseNode? FindNodeById(BaseNode root, string id)
{
    if (root.Id == id) return root;
    
    foreach (var child in root.Children)
    {
        var found = FindNodeById(child, id);
        if (found != null) return found;
    }
    
    return null;
}
```

---

## Execution Flow

When user clicks an ExecuteNode in the UI:

1. **UI Event** → `CodeExecuteViewModel.ExecuteCommand(BaseNode node)`
2. **Check if Executable** → `node.IsExecutable` (must be `ExecuteNode` with non-null `ExecutionStrategy`)
3. **Execute** → `node.Execute()` calls `ExecutionStrategy.Execute()`
4. **Strategy Routes to Provider**:
   - `DotNetExecutionStrategy` → Invokes reflected `IExternalCommand.Execute()`
   - `PythonExecutionStrategy` → Runs script via Python.NET with PEP 723 dependency resolution
5. **Update UI State**:
   - `node.IsLastExecuted = true`
   - `node.LastExecutedTime = DateTime.Now`
   - UI highlights last executed node
6. **Persist State** → `TreeStateManager.SaveState()` (async)

See [12-strategy-pattern.md](12-strategy-pattern.md) for execution strategy details.

---

## Related Documentation

- **Provider Pattern**: [11-provider-pattern.md](11-provider-pattern.md) - How providers discover and create nodes
- **Strategy Pattern**: [12-strategy-pattern.md](12-strategy-pattern.md) - How ExecutionStrategy executes nodes
- **System Design**: [10-system-design.md](10-system-design.md) - Overall architecture overview
- **Python Implementation**: [04-Python-Implementation-Details.md](04-Python-Implementation-Details.md) - Python-specific node handling
- **Source Code**: 
  - [BaseNode.cs](../../../source/RevitDevTool/CodeExecute/Models/BaseNode.cs)
  - [Nodes.cs](../../../source/RevitDevTool/CodeExecute/Models/Nodes.cs)
  - [TreeStateManager.cs](../../../source/RevitDevTool/CodeExecute/TreeStateManager.cs)
  - [TreeNodeOperations.cs](../../../source/RevitDevTool/CodeExecute/TreeNodeOperations.cs)

---

## Summary

The Tree Model provides a **simple, hierarchical organization** for commands and scripts:
- **BaseNode**: Common functionality (children, UI state, execution)
- **RootNode**: Assembly or root folder
- **IntermediateNode**: Namespace or subfolder grouping
- **ExecuteNode**: Actual executable command or script

This structure enables:
- ✅ Unified UI for .NET and Python
- ✅ Hierarchical organization
- ✅ Search and filtering
- ✅ Execution tracking
- ✅ State persistence

Future workflow-graph features (DataNode, TransformNode, etc.) are planned but not yet implemented.
