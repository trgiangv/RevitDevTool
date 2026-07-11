using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;

namespace RevitMcpToolSet.Tools;

[McpServerToolType]
[Description("Tools for host and document health status.")]
public static class ProjectTools
{
    [McpServerTool(Name = "revit_get_status", Title = "Get Revit Status", ReadOnly = true)]
    [Description("Returns host health, active document info, worksharing state, and selection count.")]
    public static object GetStatus()
    {
        try
        {
            var doc = RevitContext.ActiveDocument;
            if (doc is null)
                return new { healthy = false };

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

            var selectionCount = uiDoc?.Selection.GetElementIds().Count;
            var filePath = string.IsNullOrWhiteSpace(doc.PathName) ? null : doc.PathName;

            return new
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
        }
        catch
        {
            return new { healthy = false };
        }
    }
}
