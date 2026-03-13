using System.Text.Json;
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace RevitDevTool.McpParser.Models;

internal sealed record PythonBindingInfo
{
    public string ContainerType { get; init; } = string.Empty;
    public string MethodName { get; init; } = string.Empty;
    public string SourcePath { get; init; } = string.Empty;
}

internal sealed record PythonParsedToolEntry
{
    public JsonElement Protocol { get; init; }
    public PythonBindingInfo Binding { get; init; } = new();
}

internal sealed record PythonParsedPromptEntry
{
    public JsonElement Protocol { get; init; }
    public PythonBindingInfo Binding { get; init; } = new();
}

internal sealed record PythonParsedResourceEntry
{
    public JsonElement Protocol { get; init; }
    public bool IsTemplate { get; init; }
    public PythonBindingInfo Binding { get; init; } = new();
}

internal sealed record PythonParsedCatalog
{
    public IReadOnlyList<PythonParsedToolEntry> Tools { get; init; } = [];
    public IReadOnlyList<PythonParsedPromptEntry> Prompts { get; init; } = [];
    public IReadOnlyList<PythonParsedResourceEntry> Resources { get; init; } = [];
}
