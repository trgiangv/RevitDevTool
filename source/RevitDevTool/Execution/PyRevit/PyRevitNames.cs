namespace RevitDevTool.Execution.PyRevit;

internal static class PyRevitNames
{
    internal const string LoaderAssembly = "PyRevitLoader";
    internal const string RuntimePrefix = "PyRevitLabs.PyRevit.Runtime";
    internal const string ScriptExecutor = RuntimePrefix + ".ScriptExecutor";
    internal const string ScriptData = RuntimePrefix + ".ScriptData";
    internal const string ScriptRuntimeConfigs = RuntimePrefix + ".ScriptRuntimeConfigs";
    internal const string ScriptExecutorConfigs = RuntimePrefix + ".ScriptExecutorConfigs";
    internal const string DomainStorageKeys = RuntimePrefix + ".DomainStorageKeys";
    internal const string LoaderScriptExecutor = LoaderAssembly + ".ScriptExecutor";
    internal const string IronPythonEngine = "IronPythonEngine";
    internal const string DefaultEnginesDictKey = "PYREVITCachedEngines";

    internal const string Initialize = "Initialize";
    internal const string ExecuteScript = "ExecuteScript";
    internal const string EnginesDictKey = "EnginesDictKey";
    internal const string TypeId = "TypeId";
    internal const string Engine = "Engine";
    internal const string Message = "Message";
    internal const string Succeeded = "Succeeded";

    internal const string ScriptPath = "ScriptPath";
    internal const string ConfigScriptPath = "ConfigScriptPath";
    internal const string CommandUniqueId = "CommandUniqueId";
    internal const string CommandControlId = "CommandControlId";
    internal const string CommandName = "CommandName";
    internal const string CommandBundle = "CommandBundle";
    internal const string CommandExtension = "CommandExtension";
    internal const string CommandContext = "CommandContext";
    internal const string HelpSource = "HelpSource";
    internal const string Tooltip = "Tooltip";

    internal const string UiApp = "UIApp";
    internal const string CommandData = "CommandData";
    internal const string SelectedElements = "SelectedElements";
    internal const string SearchPaths = "SearchPaths";
    internal const string Arguments = "Arguments";
    internal const string Variables = "Variables";
    internal const string EngineConfigs = "EngineConfigs";
    internal const string RefreshEngine = "RefreshEngine";
    internal const string DebugMode = "DebugMode";
    internal const string ConfigMode = "ConfigMode";
    internal const string ExecutedFromUi = "ExecutedFromUI";
    internal const string SuppressOutput = "SuppressOutput";
}
