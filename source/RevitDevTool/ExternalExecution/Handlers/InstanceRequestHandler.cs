using System.Diagnostics;
using System.Text.Json;
using RevitDevTool.Core;
using DevTool.McpParser.Models;

namespace RevitDevTool.ExternalExecution.Handlers;

public sealed class InstanceRequestHandler
{
    private string _documentTitle = string.Empty;
    private string _documentPath = string.Empty;

    public void InitializeFromContext()
    {
        try
        {
            var document = RevitContext.ActiveDocument;
            if (document is null)
                return;

            _documentTitle = document.Title ?? string.Empty;
            _documentPath = document.PathName ?? string.Empty;
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
        VersionNumber = RevitContext.Application.VersionNumber,
        DocumentTitle = _documentTitle,
        DocumentPath = _documentPath
    };
}