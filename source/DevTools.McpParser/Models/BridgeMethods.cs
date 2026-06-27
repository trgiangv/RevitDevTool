namespace DevTools.McpParser.Models;



public static class BridgeMethods

{

    // Re-export from DevTools.Ipc

    public const string InstanceInfo = IpcBridgeMethods.InstanceInfo;

    public const string NotifyDocumentChanged = IpcBridgeMethods.NotifyDocumentChanged;



    // Re-export from DevTools.Mcp

    public const string ToolsList = McpBridgeMethods.ToolsList;

    public const string ToolsCall = McpBridgeMethods.ToolsCall;

    public const string PromptsList = McpBridgeMethods.PromptsList;

    public const string PromptsGet = McpBridgeMethods.PromptsGet;

    public const string ResourcesList = McpBridgeMethods.ResourcesList;

    public const string ResourceTemplatesList = McpBridgeMethods.ResourceTemplatesList;

    public const string ResourcesRead = McpBridgeMethods.ResourcesRead;

    public const string NotifyToolsChanged = McpBridgeMethods.NotifyToolsChanged;



    // Re-export from DevTools.Pytest

    public const string TestsDiscover = PytestBridgeMethods.TestsDiscover;

    public const string TestsRun = PytestBridgeMethods.TestsRun;

    public const string NotifyTestProgress = PytestBridgeMethods.NotifyTestProgress;

}

