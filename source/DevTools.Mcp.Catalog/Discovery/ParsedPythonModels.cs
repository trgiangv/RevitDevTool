using System.Text.Json;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace DevTools.Mcp.Catalog.Discovery;

public sealed record PythonBindingInfo
{
    public string ContainerType { get; init; } = string.Empty;
    public string MethodName { get; init; } = string.Empty;
    public string SourcePath { get; init; } = string.Empty;
}

public sealed record PythonParsedToolEntry
{
    public JsonElement Protocol { get; init; }
    public PythonBindingInfo Binding { get; init; } = new();
}

public sealed record PythonParsedResourceEntry
{
    public JsonElement Protocol { get; init; }
    public bool IsTemplate { get; init; }
    public PythonBindingInfo Binding { get; init; } = new();
}

public sealed record PythonParsedCatalog
{
    public IReadOnlyList<PythonParsedToolEntry> Tools { get; init; } = [];
    public IReadOnlyList<PythonParsedResourceEntry> Resources { get; init; } = [];
}
