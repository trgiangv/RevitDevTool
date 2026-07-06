namespace DevTools.Mcp.Models;

/// <summary>
/// JSON property names specific to MCP protocol payloads, tool schemas, and content.
/// Shared wire protocol fields (type, name, arguments, hostApp, etc.) live in
/// <see cref="IpcPropertyNames"/> to avoid cross-layer duplication.
/// </summary>
public static class McpPropertyNames
{
    // MCP tool arguments (not in IPC layer)
    public const string FilePath = "filePath";
    public const string HostInstanceId = "hostInstanceId";
    public const string LanguageCode = "languageCode";

    // MCP content model
    public const string Content = "content";
    public const string Text = "text";

    // JSON Schema
    public const string Description = "description";
    public const string Properties = "properties";
    public const string Required = "required";
    public const string Title = "title";
}
