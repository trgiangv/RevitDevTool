using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using RevitMcpToolSet.Utilities;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Data;
namespace RevitMcpToolSet.Tools;

[McpServerToolType]
[Description("Tools for annotating elements in Revit views, such as tagging and color overrides.")]
[PublicAPI]
public static class AnnotationTools
{
    [McpServerTool(Name = "revit_place_tags", Title = "Place Tags", ReadOnly = false)]
    [Description("Places tags on elements in specified views.")]
    public static object PlaceTags(
        [Description("View-element pairs to tag")] TagPlacement[] taggingData)
    {
        if (taggingData.Length == 0) throw new McpException("No tagging data provided.");

        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var tagged = 0;
        var failures = new List<object>();

        using var txGroup = new TransactionGroup(doc, "Tag Elements");
        txGroup.Start();
        try
        {
            foreach (var td in taggingData)
            {
                var view = doc.GetElement(td.ViewId.ToElementId()) as View
                    ?? throw new McpException($"View {td.ViewId} not found.");

                using var tx = new Transaction(doc, $"Tag elements in view {view.Name}");
                tx.Start();
                foreach (var eid in td.ElementsIds)
                {
                    try
                    {
                        var element = doc.GetElement(eid.ToElementId());
                        if (element is null) continue;

                        var location = element.Location as LocationPoint;
                        var tagPoint = location?.Point ?? XYZ.Zero;

                        IndependentTag.Create(doc, view.Id, new Reference(element), false,
                            TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, tagPoint);
                        tagged++;
                    }
                    catch (Exception ex)
                    {
                        failures.Add(new { elementId = eid, message = ex.Message });
                    }
                }
                tx.Commit();
            }
            txGroup.Assimilate();
        }
        catch (Exception ex)
        {
            txGroup.RollBack();
            throw new McpException($"Failed to tag elements: {ex.Message}");
        }

        return new
        {
            outcome = failures.Count == 0 ? "Success" : "Partial",
            taggedCount = tagged,
            failures = failures.Take(5),
        };
    }

    [McpServerTool(Name = "revit_override_colors", Title = "Override Element Colors", ReadOnly = false)]
    [Description("Applies a solid color graphic override to elements in the active view.")]
    public static object OverrideColors(
        [Description("Element IDs to color")] long[] elementIds,
        [Description("Color as [R, G, B] bytes")] int[] color)
    {
        if (elementIds.Length == 0) throw new McpException("No element IDs provided.");
        if (color.Length < 3) throw new McpException("Color must have 3 components [R, G, B].");

        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var activeView = RevitContext.ActiveView ?? throw new McpException("No active view.");

        var revitColor = new Color((byte)color[0], (byte)color[1], (byte)color[2]);

        var solidFillPatternId = new FilteredElementCollector(doc)
            .OfClass(typeof(FillPatternElement))
            .Cast<FillPatternElement>()
            .FirstOrDefault(fp => fp.GetFillPattern().IsSolidFill)?.Id ?? ElementId.InvalidElementId;

        using var tx = new Transaction(doc, "Color Elements");
        tx.Start();
        foreach (var eid in elementIds)
        {
            var overrideSettings = activeView.GetElementOverrides(eid.ToElementId());
            overrideSettings.SetSurfaceForegroundPatternColor(revitColor);
            overrideSettings.SetSurfaceForegroundPatternVisible(true);
            if (solidFillPatternId != ElementId.InvalidElementId)
                overrideSettings.SetSurfaceForegroundPatternId(solidFillPatternId);
            activeView.SetElementOverrides(eid.ToElementId(), overrideSettings);
        }
        tx.Commit();
        return new { status = "Success", coloredCount = elementIds.Length };
    }
}
