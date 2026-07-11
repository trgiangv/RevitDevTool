using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Data;
using RevitMcpToolSet.Utilities;

namespace RevitMcpToolSet.Tools;

[McpServerToolType]
[Description("Tools for generating grids and levels in Revit.")]
[PublicAPI]
public static class GridLevelTools
{
    [McpServerTool(Name = "revit_generate_grids", Title = "Generate Grid System", ReadOnly = false)]
    [Description("Generates a grid system with vertical and horizontal grid lines.")]
    public static object GenerateGrids(
        [Description("Vertical grid axis: count and spacing in feet.")] GridAxisSpec vertical,
        [Description("Horizontal grid axis: count and spacing in feet.")] GridAxisSpec horizontal,
        [Description("Grid origin [X, Y, Z] in feet. Defaults to [0, 0, 0].")] double[]? origin = null)
    {
        if (vertical.Count <= 0 || horizontal.Count <= 0)
            throw new McpException("Grid counts must be positive.");
        if (vertical.Spacing <= 0 || horizontal.Spacing <= 0)
            throw new McpException("Grid spacing must be positive.");

        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var coords = origin is { Length: >= 3 } ? origin : [0.0, 0.0, 0.0];
        var ox = coords[0];
        var oy = coords[1];
        var oz = coords[2];

        var horizontalExtent = horizontal.Count > 1
            ? horizontal.Spacing * (horizontal.Count - 1)
            : Math.Max(vertical.Spacing, 100.0);
        var verticalExtent = vertical.Count > 1
            ? vertical.Spacing * (vertical.Count - 1)
            : Math.Max(horizontal.Spacing, 100.0);

        using var tx = new Transaction(doc, "MCP: revit_generate_grids");
        tx.Start();
        try
        {
            var gridIds = new List<long>();

            for (var i = 0; i < vertical.Count; i++)
            {
                var x = ox + i * vertical.Spacing;
                var start = new XYZ(x, oy, oz);
                var end = new XYZ(x, oy + horizontalExtent, oz);
                var grid = Grid.Create(doc, Line.CreateBound(start, end));
                grid.Name = (i + 1).ToString();
                gridIds.Add(grid.Id.ToValue());
            }

            for (var j = 0; j < horizontal.Count; j++)
            {
                var y = oy + j * horizontal.Spacing;
                var start = new XYZ(ox, y, oz);
                var end = new XYZ(ox + verticalExtent, y, oz);
                var grid = Grid.Create(doc, Line.CreateBound(start, end));
                grid.Name = ((char)('A' + j)).ToString();
                gridIds.Add(grid.Id.ToValue());
            }

            tx.Commit();
            return new { gridIds };
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw new McpException($"Failed to create grid system: {ex.Message}");
        }
    }

    [McpServerTool(Name = "revit_generate_levels", Title = "Generate Multiple Levels", ReadOnly = false)]
    [Description("Creates levels from a list of level specifications.")]
    public static object GenerateLevels(
        [Description("Level specifications")] LevelSpec[] levels)
    {
        if (levels.Length == 0) throw new McpException("No level configurations provided.");

        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var viewFamilyType = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>().FirstOrDefault(vft => vft.ViewFamily == ViewFamily.FloorPlan);

        using var tx = new Transaction(doc, "MCP: revit_generate_levels");
        tx.Start();
        try
        {
            var levelIds = new List<long>();
            foreach (var levelInput in levels)
            {
                if (string.IsNullOrWhiteSpace(levelInput.LevelName))
                    throw new McpException("Each level specification must include a name.");

                var existingLevel = new FilteredElementCollector(doc).OfClass(typeof(Level))
                    .Cast<Level>().FirstOrDefault(l => l.Name.Equals(levelInput.LevelName, StringComparison.OrdinalIgnoreCase));

                Level level;
                if (existingLevel is not null)
                {
                    level = existingLevel;
                }
                else
                {
                    level = Level.Create(doc, levelInput.Elevation);
                    level.Name = levelInput.LevelName;
                }

                if (levelInput.CreateFloorPlanView && viewFamilyType is not null)
                {
                    var viewPlan = ViewPlan.Create(doc, viewFamilyType.Id, level.Id);
                    viewPlan.Name = $"Floor Plan - {level.Name}";
                }

                levelIds.Add(level.Id.ToValue());
            }

            tx.Commit();
            return new { levelIds };
        }
        catch (McpException)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw;
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw new McpException($"Failed to create levels: {ex.Message}");
        }
    }
}
