using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Mcp;
using RevitMcpToolSet.Utilities;

namespace RevitMcpToolSet.Tools;

[McpServerToolType]
[Description("Tools for saving, closing, and syncing the active Revit document.")]
public static class DocumentManagementTools
{
    [McpServerTool(Name = "revit_save_document", Title = "Save Document", ReadOnly = false)]
    [Description("Saves the active document in place or to a new file path.")]
    public static object SaveDocument(
        [Description("Target file path for SaveAs. Omit or null to save in place.")] string? filePath = null)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                doc.Save();
                return new { saved = true, filePath = doc.PathName };
            }

            var targetPath = PathGuard.SanitizeFilePath(filePath!);
            var options = new SaveAsOptions { OverwriteExistingFile = true };
            if (doc.IsWorkshared)
            {
                var worksharingOptions = new WorksharingSaveAsOptions { SaveAsCentral = true };
                options.SetWorksharingOptions(worksharingOptions);
            }

            doc.SaveAs(targetPath, options);
            return new { saved = true, filePath = targetPath };
        }
        catch (Exception ex)
        {
            throw new McpException($"Failed to save document: {ex.Message}");
        }
    }

    [McpServerTool(Name = "revit_close_document", Title = "Close Document", ReadOnly = false)]
    [Description("Closes the active document, optionally saving changes first.")]
    public static object CloseDocument(
        [Description("Whether to save before closing. Defaults to false.")] bool? save = null)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        try
        {
            doc.Close(save ?? false);
            return new { closed = true };
        }
        catch (Exception ex)
        {
            throw new McpException($"Failed to close document: {ex.Message}");
        }
    }

    [McpServerTool(Name = "revit_sync_with_central", Title = "Sync With Central", ReadOnly = false)]
    [McpMeta(McpTaskExecutionMeta.MetaKey, McpTaskExecutionMeta.Mode.Optional)]
    [Description("Synchronizes a workshared document with the central model.")]
    public static object SyncWithCentral(
        [Description("Sync comment recorded in the worksharing history.")] string? comment = null,
        [Description("Whether to compact the central model during sync.")] bool? compact = null,
        [Description("Whether to relinquish all borrowed elements and worksets. Defaults to false.")] bool? relinquishAll = null,
        [Description("Whether to save the local file before syncing. Defaults to true.")] bool? saveLocalBefore = null)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        if (!doc.IsWorkshared)
            throw new McpException("Document is not workshared. Use revit_save_document instead.");

        try
        {
            var transactOptions = new TransactWithCentralOptions();
            var syncOptions = new SynchronizeWithCentralOptions
            {
                Comment = comment ?? "",
                Compact = compact ?? false,
                SaveLocalBefore = saveLocalBefore ?? true,
            };

            if (relinquishAll == true)
                syncOptions.SetRelinquishOptions(new RelinquishOptions(true));

            doc.SynchronizeWithCentral(transactOptions, syncOptions);
            return new { synced = true };
        }
        catch (Exception ex)
        {
            throw new McpException($"Failed to sync with central: {ex.Message}");
        }
    }
}
