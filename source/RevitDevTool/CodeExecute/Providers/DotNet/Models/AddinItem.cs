namespace RevitDevTool.CodeExecute.Providers.DotNet.Models;

/// <summary>
/// Represents a single Command add-in item
/// </summary>
public class AddinItem(string assemblyPath, string fullClassName)
{
    public string AssemblyPath { get; } = assemblyPath;

    public string FullClassName { get; } = fullClassName;

    public string Name { get; } = fullClassName[(fullClassName.LastIndexOf('.') + 1)..];
}