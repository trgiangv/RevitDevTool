using System.Collections.ObjectModel;
using RevitDevTool.CodeExecute.Shared.Models;

namespace RevitDevTool.CodeExecute.Python.Models;

/// <summary>
/// Represents a Python script file in the tree hierarchy (leaf node)
/// </summary>
public class ScriptNode(string filePath) : TreeNodeBase
{
    /// <summary>
    /// Display name shown in the tree
    /// </summary>
    public override string DisplayName { get; } = System.IO.Path.GetFileName(filePath);

    /// <summary>
    /// Full path to the Python script file
    /// </summary>
    public string FilePath { get; } = filePath;

    /// <summary>
    /// Dynamo script item for managing .dyn file creation/caching
    /// </summary>
    public DynamoScriptItem DynamoScriptItem { get; } = new(filePath);

    public override ObservableCollection<TreeNodeBase> Children => [];
}
