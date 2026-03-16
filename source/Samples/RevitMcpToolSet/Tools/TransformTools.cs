using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Utilities;
namespace RevitMcpToolSet.Tools;

[McpServerToolType]
[Description("Tools for geometric transformations on Revit elements: removal, translation, rotation, and selection.")]
[PublicAPI]
public static class TransformTools
{
    [McpServerTool(Name = "revit_remove_elements", Title = "Remove Elements", ReadOnly = false)]
    [Description("Permanently removes elements from the model by their IDs.")]
    public static object RemoveElements(
        [Description("Array of element IDs to remove")] long[] elementIds)
    {
        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");
        if (elementIds.Length == 0) throw new McpException("No element IDs provided.");

        using var tx = new Transaction(doc, "Remove Elements");
        tx.Start();
        var failures = new List<object>();
        var deleted = 0;
        foreach (var eid in elementIds)
        {
            try
            {
                doc.Delete(eid.ToElementId());
                deleted++;
            }
            catch (Exception ex)
            {
                failures.Add(new { elementId = eid, message = ex.Message });
            }
        }
        tx.Commit();
        return new
        {
            outcome = failures.Count == 0 ? "Success" : "Partial",
            deletedCount = deleted,
            failures = failures.Take(5),
            additionalFailures = Math.Max(0, failures.Count - 5),
        };
    }

    [McpServerTool(Name = "revit_translate_elements", Title = "Translate Elements", ReadOnly = false)]
    [Description("Moves elements by a translation vector in feet.")]
    public static object TranslateElements(
        [Description("Array of element IDs to move")] long[] elementIds,
        [Description("Translation vector [X, Y, Z] in feet")] double[] translationVector)
    {
        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");
        if (elementIds.Length == 0) throw new McpException("No element IDs provided.");
        if (translationVector.Length != 3) throw new McpException("Translation vector must have exactly 3 components [X, Y, Z].");

        var translation = new XYZ(translationVector[0], translationVector[1], translationVector[2]);
        using var tx = new Transaction(doc, "Translate Elements");
        tx.Start();
        foreach (var eid in elementIds)
            ElementTransformUtils.MoveElement(doc, eid.ToElementId(), translation);
        tx.Commit();
        return new { outcome = $"Moved {elementIds.Length} elements." };
    }

    [McpServerTool(Name = "revit_rotate_elements", Title = "Rotate Elements", ReadOnly = false)]
    [Description("Rotates elements around a specified axis by a given angle in degrees.")]
    public static object RotateElements(
        [Description("Array of element IDs to rotate")] long[] elementIds,
        [Description("Axis origin [X, Y, Z]. If null, uses each element's location.")] double[]? axisOrigin,
        [Description("Axis direction [X, Y, Z]")] double[] axisDirection,
        [Description("Rotation angle in degrees")] double degrees)
    {
        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");
        if (elementIds.Length == 0) throw new McpException("No element IDs provided.");
        if (axisDirection.Length != 3) throw new McpException("Axis direction must have exactly 3 components.");

        var radians = degrees * Math.PI / 180.0;
        var direction = new XYZ(axisDirection[0], axisDirection[1], axisDirection[2]);
        var rotated = 0;

        using var tx = new Transaction(doc, "Rotate Elements");
        tx.Start();
        foreach (var eid in elementIds)
        {
            var element = doc.GetElement(eid.ToElementId());
            if (element is null) continue;

            XYZ origin;
            if (axisOrigin is { Length: >= 3 })
            {
                origin = new XYZ(axisOrigin[0], axisOrigin[1], axisOrigin[2]);
            }
            else
            {
                var location = element.Location as LocationPoint;
                origin = location?.Point ?? XYZ.Zero;
            }

            var axis = Line.CreateBound(origin, origin + direction);
            ElementTransformUtils.RotateElement(doc, element.Id, axis, radians);
            rotated++;
        }
        tx.Commit();
        return new { outcome = $"Rotated {rotated} elements by {degrees} degrees.", rotatedCount = rotated };
    }

    [McpServerTool(Name = "revit_highlight_elements", Title = "Highlight Elements in UI", ReadOnly = false)]
    [Description("Selects (highlights) elements in the Revit UI by their IDs.")]
    public static object HighlightElements(
        [Description("Array of element IDs to select")] long[] elementIds)
    {
        var uiDoc = Context.ActiveUiDocument ?? throw new McpException("No active document.");
        if (elementIds.Length == 0) throw new McpException("No element IDs provided.");

        var ids = elementIds.Select(id => id.ToElementId()).ToList();
        uiDoc.Selection.SetElementIds(ids);
        return new { outcome = $"Selected {ids.Count} elements." };
    }
}
