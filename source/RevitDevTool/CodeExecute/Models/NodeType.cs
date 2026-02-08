namespace RevitDevTool.CodeExecute.Models;

/// <summary>
/// Type of node in the tree structure
/// </summary>
public enum NodeType
{
    /// <summary>
    /// Container node (Assembly, Namespace, Folder)
    /// </summary>
    Container,

    /// <summary>
    /// Executable node (Command, Script, Graph)
    /// </summary>
    Executable
}