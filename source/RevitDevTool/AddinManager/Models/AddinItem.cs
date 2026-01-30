using Autodesk.Revit.Attributes;
using System.IO;

namespace RevitDevTool.AddinManager.Models;

/// <summary>
/// Represents a single add-in item (Command or Application)
/// </summary>
public class AddinItem(
    string assemblyPath,
    string fullClassName,
    AddinType type,
    TransactionMode? transactionMode,
    RegenerationOption? regenerationOption,
    JournalingMode? journalingMode)
{
    public AddinType AddinType { get; set; } = type;

    public string AssemblyPath { get; set; } = assemblyPath;

    public string AssemblyName { get; set; } = Path.GetFileName(assemblyPath);

    public string FullClassName { get; set; } = fullClassName;

    public string Name { get; set; } = fullClassName[(fullClassName.LastIndexOf('.') + 1)..];

    public TransactionMode? TransactionMode { get; set; } = transactionMode;

    public RegenerationOption? RegenerationMode { get; set; } = regenerationOption;

    public JournalingMode? JournalingMode { get; set; } = journalingMode;

    public override string ToString() => Name;
}

public enum AddinType
{
    Command,
    Application
}
