namespace DevTools.McpParser.Models;

public static class BridgeMethods
{
    // Re-export from DevTools.Ipc
    public const string InstanceInfo = IpcBridgeMethods.InstanceInfo;
    public const string NotifyDocumentChanged = IpcBridgeMethods.NotifyDocumentChanged;

    // MCP-specific (will move to DevTools.Mcp later)
    public const string ToolsList = "tools/list";
    public const string ToolsCall = "tools/call";
    public const string PromptsList = "prompts/list";
    public const string PromptsGet = "prompts/get";
    public const string ResourcesList = "resources/list";
    public const string ResourceTemplatesList = "resource_templates/list";
    public const string ResourcesRead = "resources/read";
    public const string NotifyToolsChanged = "notifications/tools/list_changed";

    // Pytest-specific (will move to DevTools.Pytest later)
    public const string TestsDiscover = "tests/discover";
    public const string TestsRun = "tests/run";
    public const string NotifyTestProgress = "notifications/tests/progress";
}
