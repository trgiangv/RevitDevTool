# Provider Pattern - Execution Providers

## Overview

The Provider pattern decouples language detection from code execution. Providers determine which files they can handle and create appropriate execution strategies.

**Prerequisites:** Read [10-architecture-overview.md](10-architecture-overview.md)

## IExecutionProvider Interface

**Source:** `IExecutionProvider.cs` in `RevitDevTool.CodeExecute` namespace

```csharp
public interface IExecutionProvider
{
    string ProviderId { get; }           // Unique ID: "python", "dotnet"
    string DisplayName { get; }          // UI name: "Python 3.x"
    int Priority { get; }                // Higher checked first
    
    bool CanHandle(string filePath);
    IExecutionStrategy GetStrategy(string filePath);
    Task<VersionInfo> GetVersionInfoAsync();
}
```

**Key responsibilities:**
- **CanHandle**: Determine if provider can execute this file (by extension, content, metadata)
- **GetStrategy**: Create execution strategy instance for the file
- **Priority**: Control provider precedence when multiple providers claim support

## Provider Selection Flow

```
File Path Input
    ↓
LanguageProviderFactory.GetProvider(filePath)
    ↓
For each provider (sorted by Priority descending):
    ├─ provider.CanHandle(filePath) ?
    │   ├─ YES → Return provider
    │   └─ NO  → Try next provider
    ↓
If no provider: Throw UnsupportedFileException
```

### Priority Levels

Standard priority values:

| Priority | Usage |
|----------|-------|
| 100+ | Primary providers (Python, C#) |
| 50-99 | Secondary providers |
| 1-49 | Fallback providers |
| 0 | Disabled |

## Provider Implementations

### PythonExecutionProvider

**Source:** `PythonExecutionProvider.cs` in `RevitDevTool.CodeExecute.Python` namespace

**Priority:** 100 (checked first)

**CanHandle logic:**
1. File extension is `.py`
2. File exists and is readable
3. (Optional) Contains Python syntax: `import`, `from`, `def`, `class`
4. (Bonus) Contains PEP 723 metadata block

**GetStrategy:**
- Returns `PythonExecutionStrategy` instance
- Passes Python executor and dependency manager
- Configures for in-process execution via Python.NET

### DotNetExecutionProvider

**Source:** `DotNetExecutionProvider.cs` in `RevitDevTool.CodeExecute.DotNet` namespace

**Priority:** 90

**CanHandle logic:**
1. File extension is `.cs` or `.csx`
2. File exists and is readable
3. (Optional) Contains C# keywords: `using`, `class`, `namespace`
4. (Bonus) Has companion `.csproj` file

**GetStrategy:**
- Returns `DotNetExecutionStrategy` instance
- Configures for subprocess execution via `dotnet run`
- Passes .NET SDK path from settings

### (Future) NodeExecutionProvider

Placeholder for JavaScript/TypeScript support:

**Priority:** 85

**CanHandle logic:**
1. File extension is `.js` or `.ts`
2. Contains Node.js markers: `require()`, `module.exports`, `import`
3. Has `package.json` in directory

**GetStrategy:**
- Returns `NodeExecutionStrategy` instance
- Configures subprocess execution via `node` runtime

## LanguageProviderFactory

**Source:** `LanguageProviderFactory.cs` in `RevitDevTool.CodeExecute` namespace

Central registry and selector for providers.

**Key methods:**
```csharp
public static class LanguageProviderFactory
{
    void RegisterProvider(IExecutionProvider provider);
    IExecutionProvider GetProvider(string filePath);
    IEnumerable<IExecutionProvider> GetAllProviders();
}
```

**Registration pattern:**
```csharp
// During application startup
LanguageProviderFactory.RegisterProvider(new PythonExecutionProvider(...));
LanguageProviderFactory.RegisterProvider(new DotNetExecutionProvider(...));
```

**Selection pattern:**
```csharp
// When executing a file
var provider = LanguageProviderFactory.GetProvider("script.py");
var strategy = provider.GetStrategy("script.py");
var result = await strategy.ExecuteAsync();
```

## Provider Architecture

### Composition

Providers typically depend on:

**Executor**: Runtime-specific execution engine
- `PythonExecutor` - Python.NET wrapper
- `DotNetExecutor` - dotnet CLI wrapper
- `NodeExecutor` - node CLI wrapper

**Dependency Manager**: Handles package installation
- `PythonDependencyManager` - uv/pip integration
- `DotNetDependencyManager` - NuGet restore
- `NodeDependencyManager` - npm install

**Settings Service**: Runtime configuration
- Runtime paths
- Timeout values
- Auto-install preferences

### Example Structure

```
PythonExecutionProvider
    ├─ Depends on PythonExecutor
    ├─ Depends on PythonDependencyManager
    ├─ Implements CanHandle (extension + syntax check)
    └─ Creates PythonExecutionStrategy
        ├─ Uses PythonExecutor.Execute()
        ├─ Parses PEP 723 for dependencies
        └─ Returns ExecutionResult
```

## File Detection Strategies

### Extension-Only

Simple, fast, but can have false positives:

```csharp
public bool CanHandle(string filePath)
{
    return Path.GetExtension(filePath)
        .Equals(".py", StringComparison.OrdinalIgnoreCase);
}
```

### Extension + Content

More reliable, checks file syntax:

```csharp
public bool CanHandle(string filePath)
{
    if (!filePath.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
        return false;
    
    var content = File.ReadAllText(filePath);
    return content.Contains("import ") || 
           content.Contains("from ") ||
           content.Contains("def ");
}
```

### Extension + Metadata

Most reliable, uses embedded metadata:

```csharp
public bool CanHandle(string filePath)
{
    if (!filePath.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
        return false;
    
    var content = File.ReadAllText(filePath);
    return content.Contains("# /// script");  // PEP 723 marker
}
```

## Error Handling

Providers should handle errors gracefully:

```csharp
public bool CanHandle(string filePath)
{
    try
    {
        if (!File.Exists(filePath))
            return false;
        
        // Detection logic...
        return true;
    }
    catch (IOException ex)
    {
        _logger.Warning(ex, "Cannot read {FilePath}", filePath);
        return false;
    }
    catch (UnauthorizedAccessException ex)
    {
        _logger.Warning(ex, "No permission for {FilePath}", filePath);
        return false;
    }
}
```

## Best Practices

### For Provider Implementers

1. **Check extension first** - fastest filter
2. **Read file content sparingly** - only when necessary for disambiguation
3. **Handle IO errors** - file may not exist, be locked, or have permission issues
4. **Return false on doubt** - if uncertain, let other providers try
5. **Set appropriate priority** - avoid conflicts with existing providers

### For Provider Users

1. **Register all providers** before first use
2. **Handle UnsupportedFileException** when no provider available
3. **Log provider selection** for debugging
4. **Cache providers** - don't recreate on every execution
5. **Order matters** - register high-priority providers first

## Integration with Strategy Pattern

```
File Extension → Provider.CanHandle() → Provider.GetStrategy() → IExecutionStrategy
    .py       →  PythonProvider       → PythonExecutionStrategy
    .cs/.csx  →  DotNetProvider       → DotNetExecutionStrategy
    .js/.ts   →  NodeProvider         → NodeExecutionStrategy
```

See [12-strategy-pattern.md](12-strategy-pattern.md) for execution strategy details.

## Configuration

**Source:** `Settings/ProviderSettings.cs`

- `EnabledProviders`: List of active provider IDs
- `DefaultProvider`: Fallback when detection is ambiguous
- `ProviderPriorities`: Override default priorities
- `CacheProviderResults`: Cache CanHandle() results for performance
