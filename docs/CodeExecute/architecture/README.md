# CodeExecute Architecture - System Design Documentation

Welcome to the architecture documentation for RevitDevTool's **CodeExecute** plugin system. This folder contains detailed documentation for developers building custom execution providers, strategies, and extensions.

---

## 📚 Architecture Documentation

**For Developers Building Extensions**

1. **[10-system-design.md](10-system-design.md)** (10 min)
   - High-level system architecture
   - Provider and strategy patterns
   - Component responsibilities
   - Extensibility framework
   - Thread safety and error handling

2. **[11-provider-pattern.md](11-provider-pattern.md)** (12 min)
   - How to build custom execution providers
   - File detection and routing
   - Python provider deep-dive
   - Node.js provider example
   - Provider registration and lifecycle

3. **[12-strategy-pattern.md](12-strategy-pattern.md)** (10 min)
   - Execution strategy design
   - Validation and error recovery
   - Common strategy patterns:
     - Subprocess-based execution
     - In-process execution
     - Remote (IPC) execution
   - Testing strategies

4. **[13-tree-model.md](13-tree-model.md)** (8 min)
   - BaseNode composite pattern
   - Node types and hierarchy
   - Graph persistence and state management
   - Execution history tracking
   - Tree validation and visualization

5. **[04-Python-Implementation-Details.md](04-Python-Implementation-Details.md)** (15 min) ✅ NEW
   - Interface implementation in Python.NET
   - Transaction context management
   - Type conversion and nullable types
   - Event subscription patterns
   - Module reload safety
   - Performance comparison
   - Hybrid C# + Python approaches

---

## 🎯 Quick Navigation

**"I need to..."**

- **Add support for a new language (e.g., Lua, Rust)?**  
  → [11-provider-pattern.md](11-provider-pattern.md) + [12-strategy-pattern.md](12-strategy-pattern.md)

- **Understand how Python execution works?**  
  → [04-Python-Implementation-Details.md](04-Python-Implementation-Details.md) or Wiki `Python-Runtime`

- **Compare different Python approaches (RevitDevTool vs. pyRevit)?**  
  → [../50-python-execution-strategies.md](../50-python-execution-strategies.md)

- **Implement custom node types for workflow graphs?**  
  → [13-tree-model.md](13-tree-model.md)

- **Handle complex execution scenarios (timeouts, retries)?**  
  → [12-strategy-pattern.md](12-strategy-pattern.md) - Error handling section

- **Build a provider registry or discovery system?**  
  → [10-system-design.md](10-system-design.md) - Extensibility points

---

## 🏗️ Architecture Overview

```
CodeExecute System Architecture
├─ Orchestrator (Main Controller)
│  └─ Selects provider
│     └─ Creates strategy
│        ├─ Validates
│        ├─ Handles dependencies
│        └─ Executes
│
├─ Provider Pattern (File Detection)
│  ├─ Python Provider (.py files)
│  ├─ DotNet Provider (.cs files)
│  └─ Custom providers (*.custom, etc.)
│
├─ Strategy Pattern (Execution)
│  ├─ Python Strategy (PEP 723 → UV → execute)
│  ├─ DotNet Strategy (Compile → execute)
│  └─ Custom strategies (Subprocess, in-process, etc.)
│
└─ Tree Model (Workflow Graphs)
   ├─ RootNode (contains execution graph)
   ├─ ExecuteNode (execute file/code)
   ├─ TransformNode (data transformation)
   ├─ ConditionalNode (branching)
   └─ State persistence (JSON serialization)
```

---

## 📖 When to Read Each Doc

| Document | Read When | Prerequisites |
|----------|-----------|---------------|
| 10-system-design.md | Understanding overall architecture | None |
| 11-provider-pattern.md | Building a new language provider | 10-system-design.md |
| 12-strategy-pattern.md | Building custom execution logic | 10-system-design.md, 11-provider-pattern.md |
| 13-tree-model.md | Creating workflow graphs or node types | 10-system-design.md |
| 04-Python-Implementation-Details.md | Understanding Python.NET limitations | None (independent) |

---

## 🔗 Related Documentation

**User Guides:**
- `CodeExecute-Home` - Getting started (load scripts, execute)
- `Python-Runtime` - How Python execution works internally (see `../40-python-runtime.md`)
- `Dependency-Resolution` - PEP 723 and UV workflow (see `../42-dependency-flow.md`)
- `Python-Execution-Strategies` - Comparison of all Python approaches (see `../50-python-execution-strategies.md`)
- `Dashboard-Reference` - Production example patterns (see `../43-dashboard-reference.md`)

**Examples (same folder, one level up):**
- `examples/minimal_script_template.py` - Copy-paste starter
- `examples/data-analysis.md` - Data analysis walkthrough
- `examples/advanced_revit_patterns.py` - 10 production patterns

---

## 🚀 Getting Started: Custom Provider Example

**Goal:** Add support for a new language (e.g., Node.js)

**Steps:**

1. Read [10-system-design.md](10-system-design.md) to understand IExecutionProvider
2. Read [11-provider-pattern.md](11-provider-pattern.md) for implementation details
3. Create `NodeJsExecutionProvider : IExecutionProvider`
   ```csharp
   public bool CanHandle(string filePath) => filePath.EndsWith(".js");
   public IExecutionStrategy GetStrategy(string filePath) 
       => new NodeJsExecutionStrategy(filePath);
   ```
4. Create `NodeJsExecutionStrategy : IExecutionStrategy`
5. Register in dependency injection container
6. Test with `Trace.Write(some_command)`

See [11-provider-pattern.md](11-provider-pattern.md) for complete Node.js example.

---

## ⚙️ Key Interfaces

### IExecutionProvider
```csharp
public interface IExecutionProvider
{
    string ProviderId { get; }
    int Priority { get; }
    bool CanHandle(string filePath);
    IExecutionStrategy GetStrategy(string filePath);
}
```
→ Identifies which execution environment to use

### IExecutionStrategy
```csharp
public interface IExecutionStrategy
{
    Task<ValidationResult> ValidateAsync();
    Task<ExecutionResult> ExecuteAsync();
    Task<VersionInfo> GetVersionInfoAsync();
}
```
→ Defines how to execute code once provider is selected

### ITreeStateManager
```csharp
public interface ITreeStateManager
{
    Task SaveAsync(RootNode tree, string filePath);
    Task<RootNode> LoadAsync(string filePath);
}
```
→ Persist and restore execution graphs

---

## 📊 Architecture Patterns Used

- **Provider Pattern** - Select execution environment by file type
- **Strategy Pattern** - Pluggable execution algorithms
- **Composite Pattern** - BaseNode tree for workflow graphs
- **Factory Pattern** - Create providers/strategies dynamically
- **Observer Pattern** - FileWatcher → Orchestrator → UI
- **Template Method** - VisualizationServer base class

---

## 🔍 Recommended Reading Order

1. Start: [10-system-design.md](10-system-design.md) - Get the big picture
2. Then: One of:
   - [11-provider-pattern.md](11-provider-pattern.md) - If extending language support
   - [13-tree-model.md](13-tree-model.md) - If building workflow graphs
   - [12-strategy-pattern.md](12-strategy-pattern.md) - If custom execution logic
3. Finally: Examples (`../examples/`) and Wiki pages for practical context

---

**Ready to extend CodeExecute? Start with [10-system-design.md](10-system-design.md)**
