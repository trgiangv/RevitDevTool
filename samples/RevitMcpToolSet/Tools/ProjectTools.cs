using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Utilities;

namespace RevitMcpToolSet.Tools;

[McpServerToolType]
[Description("Tools for host and document health status.")]
public static class ProjectTools
{
    [McpServerTool(Name = "revit_get_status", Title = "Get Revit Status", ReadOnly = true, UseStructuredContent = true)]
    [Description("Returns host health, active document info, worksharing state, and selection count.")]
    public static CallToolResult GetStatus()
    {
        try
        {
            var doc = RevitContext.ActiveDocument;
            if (doc is null)
                return StructuredToolResults.Create(new { healthy = false }, "No active document");

            var uiDoc = RevitContext.ActiveUiDocument;
            var app = RevitContext.UiApplication?.Application;

            string? centralPath = null;
            string? activeWorkset = null;
            if (doc.IsWorkshared)
            {
                try
                {
                    var centralModelPath = doc.GetWorksharingCentralModelPath();
                    if (centralModelPath is not null)
                        centralPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(centralModelPath);
                }
                catch
                {
                    // Ignore central path lookup failures for unsaved or detached models.
                }

                try
                {
                    var worksetTable = doc.GetWorksetTable();
                    var activeWorksetId = worksetTable.GetActiveWorksetId();
                    if (activeWorksetId != WorksetId.InvalidWorksetId)
                        activeWorkset = worksetTable.GetWorkset(activeWorksetId).Name;
                }
                catch
                {
                    // Ignore workset lookup failures.
                }
            }

            var selectionCount = uiDoc?.Selection.GetElementIds().Count ?? 0;
            var filePath = string.IsNullOrWhiteSpace(doc.PathName) ? null : doc.PathName;

            var structured = new
            {
                healthy = true,
                documentTitle = doc.Title,
                filePath,
                worksharingEnabled = doc.IsWorkshared,
                centralPath,
                activeWorkset,
                selectionCount,
                version = app?.VersionNumber,
            };

            return StructuredToolResults.Create(
                structured,
                $"Model healthy, {selectionCount} selected");
        }
        catch
        {
            return StructuredToolResults.Create(new { healthy = false }, "Revit status unavailable");
        }
    }
}
