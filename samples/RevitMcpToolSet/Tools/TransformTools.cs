using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Data;
using RevitMcpToolSet.Utilities;

namespace RevitMcpToolSet.Tools;

[McpServerToolType]
[Description("Tools for geometric transformations on Revit elements: move, rotate, delete, and selection.")]
[PublicAPI]
public static class TransformTools
{
    private const int DeleteConfirmationThreshold = 50;

    [McpServerTool(Name = "revit_move_elements", Title = "Move Elements", ReadOnly = false)]
    [Description("Moves elements by a translation vector in feet.")]
    public static object MoveElements(
        [Description("Array of element IDs to move")] long[] elementIds,
        [Description("Translation vector [X, Y, Z] in feet")] double[] vector)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        if (elementIds.Length == 0) throw new McpException("No element IDs provided.");
        if (vector.Length != 3) throw new McpException("Vector must have exactly 3 components [X, Y, Z].");

        var translation = new XYZ(vector[0], vector[1], vector[2]);
        var failures = new List<ToolError>();
        var movedCount = 0;

        using var tx = new Transaction(doc, "MCP: revit_move_elements");
        tx.Start();
        foreach (var eid in elementIds)
        {
            try
            {
                var element = doc.GetElement(eid.ToElementId());
                if (element is null)
                {
                    failures.Add(ToolErrorHelper.FromMessage($"Element {eid} not found", eid));
                    continue;
                }

                ElementTransformUtils.MoveElement(doc, element.Id, translation);
                movedCount++;
            }
            catch (Exception ex)
            {
                failures.Add(ToolErrorHelper.FromException(ex, eid));
            }
        }
        tx.Commit();
        return new
        {
            moved_count = movedCount,
            failures = failures.Count > 0 ? failures : null,
        };
    }

    [McpServerTool(Name = "revit_rotate_elements", Title = "Rotate Elements", ReadOnly = false)]
    [Description("Rotates elements around a specified axis by a given angle in degrees.")]
    public static object RotateElements(
        [Description("Array of element IDs to rotate")] long[] elementIds,
        [Description("Axis origin [X, Y, Z] in feet")] double[] axisOrigin,
        [Description("Axis direction [X, Y, Z]")] double[] axisDirection,
        [Description("Rotation angle in degrees")] double degrees)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        if (elementIds.Length == 0) throw new McpException("No element IDs provided.");
        if (axisOrigin.Length != 3) throw new McpException("Axis origin must have exactly 3 components.");
        if (axisDirection.Length != 3) throw new McpException("Axis direction must have exactly 3 components.");

        var radians = degrees * Math.PI / 180.0;
        var origin = new XYZ(axisOrigin[0], axisOrigin[1], axisOrigin[2]);
        var direction = new XYZ(axisDirection[0], axisDirection[1], axisDirection[2]);
        if (direction.IsZeroLength())
            throw new McpException("Axis direction must be a non-zero vector.");

        var failures = new List<ToolError>();
        var rotatedCount = 0;

        using var tx = new Transaction(doc, "MCP: revit_rotate_elements");
        tx.Start();
        foreach (var eid in elementIds)
        {
            try
            {
                var element = doc.GetElement(eid.ToElementId());
                if (element is null)
                {
                    failures.Add(ToolErrorHelper.FromMessage($"Element {eid} not found", eid));
                    continue;
                }

                var axis = Line.CreateBound(origin, origin + direction);
                ElementTransformUtils.RotateElement(doc, element.Id, axis, radians);
                rotatedCount++;
            }
            catch (Exception ex)
            {
                failures.Add(ToolErrorHelper.FromException(ex, eid));
            }
        }
        tx.Commit();
        return new
        {
            rotated_count = rotatedCount,
            failures = failures.Count > 0 ? failures : null,
        };
    }

    [McpServerTool(Name = "revit_delete_elements", Title = "Delete Elements", ReadOnly = false)]
    [Description("Deletes elements from the model. Use dryRun=true to preview deletions including dependents.")]
    public static object DeleteElements(
        [Description("Array of element IDs to delete")] long[] elementIds,
        [Description("When true, previews deletions without committing changes")] bool dryRun = false)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        if (elementIds.Length == 0) throw new McpException("No element IDs provided.");

        if (!dryRun && elementIds.Length > DeleteConfirmationThreshold)
        {
            return new
            {
                deleted_count = 0,
                warning = $"Deleting {elementIds.Length} elements exceeds the confirmation threshold of {DeleteConfirmationThreshold}. " +
                          "Set dryRun=true to preview what would be deleted, then retry with fewer elements or after engineer confirmation.",
                failures = (ToolError[]?)null,
                dryRunResults = (object[]?)null,
            };
        }

        var failures = new List<ToolError>();
        var dryRunResults = new List<object>();
        var deletedIds = new HashSet<long>();

        using var tx = new Transaction(doc, "MCP: revit_delete_elements");
        tx.Start();
        foreach (var eid in elementIds)
        {
            try
            {
                var element = doc.GetElement(eid.ToElementId());
                if (element is null)
                {
                    failures.Add(ToolErrorHelper.FromMessage($"Element {eid} not found", eid));
                    continue;
                }

                var deleted = doc.Delete(element.Id);
                foreach (var deletedId in deleted)
                    deletedIds.Add(deletedId.ToValue());

                if (dryRun)
                {
                    dryRunResults.Add(new
                    {
                        requestedId = eid,
                        wouldDelete = deleted.Select(id => id.ToValue()).ToArray(),
                    });
                }
            }
            catch (Exception ex)
            {
                failures.Add(ToolErrorHelper.FromException(ex, eid));
            }
        }

        if (dryRun)
            tx.RollBack();
        else
            tx.Commit();

        return new
        {
            deleted_count = dryRun ? 0 : deletedIds.Count,
            failures = failures.Count > 0 ? failures : null,
            dryRunResults = dryRun && dryRunResults.Count > 0 ? dryRunResults : null,
        };
    }

    [McpServerTool(Name = "revit_highlight_elements", Title = "Highlight Elements in UI", ReadOnly = false)]
    [Description("Selects (highlights) elements in the Revit UI by their IDs.")]
    public static object HighlightElements(
        [Description("Array of element IDs to select")] long[] elementIds)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var uiDoc = RevitContext.ActiveUiDocument ?? throw new McpException("No active UI document.");
        if (elementIds.Length == 0) throw new McpException("No element IDs provided.");

        var ids = elementIds
            .Where(id => doc.GetElement(id.ToElementId()) is not null)
            .Select(id => id.ToElementId())
            .ToList();

        uiDoc.Selection.SetElementIds(ids);
        return new { selected_count = ids.Count };
    }
}
