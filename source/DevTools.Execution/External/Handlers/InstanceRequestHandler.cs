using System.Diagnostics;
using System.Text.Json;
using DevTools.Logging;
using DevTools.McpParser.Models;
namespace DevTools.Execution.External.Handlers;

public sealed class InstanceRequestHandler(IHostAppInfo hostInfo)
{
    private string _documentTitle = string.Empty;
    private string _documentPath = string.Empty;

    private Action<InstanceRequestHandler>? _contextInitializer;

    /// <summary>
    /// Registers a host-specific delegate for populating document context.
    /// </summary>
    public void SetContextInitializer(Action<InstanceRequestHandler> initializer)
        => _contextInitializer = initializer;

    public void SetDocumentInfo(string title, string path)
    {
        _documentTitle = title;
        _documentPath = path;
    }

    public void InitializeFromContext()
    {
        try
        {
            _contextInitializer?.Invoke(this);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[PipeServer] Could not read active document: {ex.Message}");
        }
    }

    public BridgeMessage HandleInstanceInfo(string id)
    {
        var json = JsonSerializer.SerializeToElement(BuildInstanceInfo());
        return BridgeMessage.Response(id, json);
    }

    private InstanceInfo BuildInstanceInfo() => new()
    {
        ProcessId = Environment.ProcessId,
        VersionNumber = hostInfo.VersionNumber,
        DocumentTitle = _documentTitle,
        DocumentPath = _documentPath
    };
}
