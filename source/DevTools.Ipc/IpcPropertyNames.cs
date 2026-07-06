namespace DevTools.Ipc;

public static class IpcPropertyNames
{
    // Bridge envelope
    public const string ErrorMessage = "errorMessage";
    public const string Id = "id";
    public const string IsError = "isError";
    public const string Method = "method";
    public const string Params = "params";
    public const string Result = "result";
    public const string Type = "type";

    // Structured error detail (BridgeError)
    public const string Code = "code";
    public const string Data = "data";
    public const string Error = "error";
    public const string Message = "message";

    // Shared wire protocol fields (used by DTOs across layers)
    public const string Arguments = "arguments";
    public const string BridgeConnected = "bridgeConnected";
    public const string HostApp = "hostApp";
    public const string Name = "name";
    public const string PipeName = "pipeName";
    public const string ProcessId = "processId";
    public const string Success = "success";
    public const string Uri = "uri";
    public const string Version = "version";
    public const string VersionNumber = "versionNumber";
}
