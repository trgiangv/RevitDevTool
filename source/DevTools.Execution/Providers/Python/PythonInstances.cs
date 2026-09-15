namespace DevTools.Execution.Providers.Python;

/// <summary>
/// Canonical names for variables exchanged between C# and embedded Python scripts.
/// Any rename here must be mirrored in the corresponding Python script constants.
/// </summary>
public static class PythonInstances
{
    // Execution scope
    public const string Source = "__source__";
    public const string File = "__file__";
    public const string Root = "__root__";

    // MCP operations
    public const string OperationResource = "resource";
    public const string Operation = "__operation__";
    public const string ToolName = "__tool_name__";
    public const string PayloadJson = "__payload_json__";
    public const string ResourceName = "__resource_name__";
    public const string ResourceUri = "__resource_uri__";
    public const string ResultJson = "__result_json__";
    public const string ToolsetDirectory = "__toolset_directory__";
    public const string ParserResult = "__parser_result__";

    // Test execution
    public const string PytestRequestJson = "__pytest_request_json__";
    public const string ProgressCallback = "__progress_callback__";

    // Debugger (CPython debugpy / IronPython pydevd)
    public const string Port = "__port__";
    public const string IsConnected = "__is_connected__";
    public const string IsAttached = "__is_attached__";
    public const string HasTrace = "__has_trace__";
    public const string TraceFunc = "__trace_func__";
    public const string Debugger = "__dbg__";
    public const string RefreshRoot = "__refresh_root__";
    public const string RefreshSkip = "__refresh_skip__";
}
