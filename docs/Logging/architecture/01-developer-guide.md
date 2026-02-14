# Logging Architecture

## Overview

The logging system is built on top of .NET's `System.Diagnostics.Trace` infrastructure with extensions for structured logging, keyword detection, and Python integration.

## Developer Workflow

1. **Initialize** - Set up logging service during application startup
2. **Write Logs** - Use `ILoggerAdapter` to emit structured log messages
3. **Add Custom Listeners** - Route logs to files, databases, cloud services, etc.

## Core Components

### ILoggingService

**Source:** `ILoggingService.cs` in `RevitDevTool.Logging` namespace

Main service interface for logging system lifecycle:

```csharp
public interface ILoggingService : IDisposable
{
    void Initialize(bool isDarkTheme);
    void RegisterTraceListeners();
    void UnregisterTraceListeners();
    void SetMinimumLevel(LogLevel level);
}
```

**Key responsibilities:**
- Initialize logging infrastructure with theme configuration
- Register/unregister built-in TraceListeners
- Control minimum log level filtering
- Clean up resources on disposal

### ILoggerAdapter

**Source:** `ILoggerAdapter.cs` in `RevitDevTool.Logging` namespace

Framework-agnostic logging interface:

```csharp
public interface ILoggerAdapter : IDisposable
{
    void Verbose(string messageTemplate, params object?[] propertyValues);
    void Debug(string messageTemplate, params object?[] propertyValues);
    void Information(string messageTemplate, params object?[] propertyValues);
    void Warning(string messageTemplate, params object?[] propertyValues);
    void Error(string messageTemplate, params object?[] propertyValues);
    void Error(Exception exception, string messageTemplate, params object?[] propertyValues);
    void Fatal(string messageTemplate, params object?[] propertyValues);
    void Fatal(Exception exception, string messageTemplate, params object?[] propertyValues);
    
    void Write(LogLevel level, string messageTemplate, params object?[] propertyValues);
    void Write(LogLevel level, Exception? exception, string messageTemplate, params object[] propertyValues);
    
    ILoggerAdapter ForContext(string propertyName, object? value);
    ILoggerAdapter ForContext<T>() where T : class;
}
```

**Key features:**
- Severity-level methods (Verbose → Fatal)
- Exception overloads for error logging
- Context enrichment via `ForContext()`
- Structured message templates with property placeholders

## Architecture Flow

1. **Application** calls `_logger.Information("Message {Property}", value)`
2. **ILoggerAdapter** formats message with properties
3. **System.Diagnostics.Trace** receives formatted message
4. **Keyword Detection** scans message for severity keywords
5. **TraceListeners** filter and route based on detected level
6. **Output** written to configured destinations (file, console, database, etc.)

## Initialization Example

```csharp
public class AppInitializer
{
    private readonly ILoggingService _loggingService;

    public void Initialize(bool isDarkTheme)
    {
        _loggingService.Initialize(isDarkTheme);
        _loggingService.RegisterTraceListeners();
        _loggingService.SetMinimumLevel(LogLevel.Information);
    }

    public void Shutdown()
    {
        _loggingService.UnregisterTraceListeners();
        _loggingService.Dispose();
    }
}
```

## Usage Pattern

```csharp
public class ElementProcessor
{
    private readonly ILoggerAdapter _logger;

    public void ProcessElement(Element element)
    {
        _logger.Information("Processing element: {ElementId}", element.Id);

        try
        {
            var contextLogger = _logger
                .ForContext("Category", element.Category?.Name)
                .ForContext("Type", element.GetType().Name);

            contextLogger.Debug("Details loaded");
            contextLogger.Information("Processing complete");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to process {ElementId}", element.Id);
        }
    }
}
```

## Custom TraceListeners

Custom listeners inherit from `System.Diagnostics.TraceListener` and override event methods:

**Common listener types:**
- **File Listeners**: Write logs to files with rotation, compression, retention policies
- **Database Listeners**: Store logs in SQL databases for querying and analysis
- **Cloud Listeners**: Send logs to remote services via HTTP/gRPC
- **Alert Listeners**: Monitor for critical keywords and send notifications
- **Colorized Listeners**: Apply ANSI color codes based on log level

**Key methods to override:**
- `TraceEvent()` - Handle structured trace events
- `Write()` / `WriteLine()` - Handle raw string output
- `Dispose()` - Clean up resources

### Registration

```csharp
// During initialization
var customListener = new MyCustomListener();
Trace.Listeners.Add(customListener);

// Later, during shutdown
Trace.Listeners.Remove(customListener);
customListener.Dispose();
```

## Context Enrichment

```csharp
// Add properties to all logs from this logger instance
var enrichedLogger = _logger
    .ForContext("UserId", currentUser.Id)
    .ForContext("SessionId", sessionId);

// All logs from enrichedLogger include UserId and SessionId
enrichedLogger.Information("User action completed");
// Output: [INFO] User action completed | UserId=123 SessionId=abc-def
```

## Exception Handling

```csharp
try
{
    PerformRiskyOperation();
}
catch (InvalidOperationException ex)
{
    _logger.Error(ex, "Invalid operation on {Resource}", resourceName);
}
catch (Exception ex)
{
    _logger.Fatal(ex, "Unexpected error in {Operation}", operationName);
    throw;
}
```

## Integration Points

- **Keyword Detection**: See [03-Theme-System.md](03-Theme-System.md)
- **Python Integration**: See [05-Python-Integration.md](05-Python-Integration.md)
- **TraceListener Filtering**: See [04-Listener-Management.md](04-Listener-Management.md)
