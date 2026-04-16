using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Utilities;
namespace RevitMcpToolSet.Tools;

[McpServerToolType]
[Description("Tools for managing external file links in the Revit document.")]
public static class LinkTools
{
    [McpServerTool(Name = "revit_attach_cad", Title = "Attach CAD File", ReadOnly = false)]
    [Description("Attaches a CAD (DWG) file into the active Revit view.")]
    public static object AttachCad(
        [Description("Full path to the DWG file")] string filePath,
        [Description("Import unit: Foot, Inch, Meter, Millimeter, Centimeter, or Auto")] string importUnit = "Auto",
        [Description("Placement: Origin, Shared, or LastPlacement")] string placement = "Origin")
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var activeView = RevitContext.ActiveView ?? throw new McpException("No active view.");

        if (!File.Exists(filePath))
            throw new McpException($"File not found: '{filePath}'");

        var unit = importUnit.ToLowerInvariant() switch
        {
            "foot" => ImportUnit.Foot,
            "inch" => ImportUnit.Inch,
            "meter" => ImportUnit.Meter,
            "millimeter" => ImportUnit.Millimeter,
            "centimeter" => ImportUnit.Centimeter,
            _ => ImportUnit.Default,
        };

        var placementMode = placement.ToLowerInvariant() switch
        {
            "shared" => ImportPlacement.Shared,
            _ => ImportPlacement.Origin,
        };

        var options = new DWGImportOptions
        {
            Unit = unit,
            Placement = placementMode,
            ThisViewOnly = true,
        };

        using var tx = new Transaction(doc, "Attach CAD File");
        tx.Start();
        try
        {
            doc.Import(filePath, options, activeView, out var elementId);
            tx.Commit();
            return new { status = "Success", elementId = elementId.ToValue() };
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw new McpException($"Failed to attach CAD file: {ex.Message}");
        }
    }

    [McpServerTool(Name = "revit_list_links", Title = "List Linked Files", ReadOnly = true)]
    [Description("Lists all linked Revit and CAD files in the current document.")]
    public static object ListLinks()
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var revitLinks = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkType))
            .Cast<RevitLinkType>()
            .Select(l => new { Type = "Revit", Id = l.Id.ToValue(), Name = l.Name })
            .ToList<object>();

        var cadImports = new FilteredElementCollector(doc).OfClass(typeof(ImportInstance))
            .Cast<ImportInstance>()
            .Select(i => new { Type = "CAD", Id = i.Id.ToValue(), Name = i.Name ?? "" })
            .ToList<object>();

        var allLinks = revitLinks.Concat(cadImports).ToList();
        return new { linkedFiles = JsonSerializer.Serialize(allLinks) };
    }
}
