namespace DevTools.Execution.Providers.Dotnet;

/// <summary>
/// Represents a single Command add-in item
/// </summary>
public class CommandItem(string assemblyPath, string fullClassName)
{
    public string AssemblyPath { get; } = assemblyPath;

    public string FullClassName { get; } = fullClassName;

    public string Name { get; init; } = fullClassName[(fullClassName.LastIndexOf('.') + 1)..];
}