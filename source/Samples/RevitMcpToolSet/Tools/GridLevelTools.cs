using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using RevitMcpToolSet.Data;
namespace RevitMcpToolSet.Tools;

[McpServerToolType]
[Description("Tools for generating and adjusting grids and levels in Revit.")]
[PublicAPI]
public static class GridLevelTools
{
    [McpServerTool(Name = "revit_generate_grids", Title = "Generate Grid System", ReadOnly = false)]
    [Description("Generates a grid system with vertical and horizontal grid lines.")]
    public static object GenerateGrids(
        [Description("Number of vertical grid lines")] int verticalCount,
        [Description("Spacing between vertical grids in feet")] double verticalSpacing,
        [Description("Number of horizontal grid lines")] int horizontalCount,
        [Description("Spacing between horizontal grids in feet")] double horizontalSpacing,
        [Description("Grid origin [X, Y, Z] in feet")] double[] origin,
        [Description("Grid extent length in feet")] double extent = 100.0)
    {
        if (origin.Length < 3) throw new McpException("Origin must have 3 components [X, Y, Z].");
        if (verticalCount <= 0 || horizontalCount <= 0) throw new McpException("Grid counts must be positive.");

        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");
        var ox = origin[0];
        var oy = origin[1];
        var oz = origin[2];

        using var tx = new Transaction(doc, "Create Grid System");
        tx.Start();
        try
        {
            var createdGrids = new List<long>();

            for (var i = 0; i < verticalCount; i++)
            {
                var x = ox + i * verticalSpacing;
                var start = new XYZ(x, oy, oz);
                var end = new XYZ(x, oy + extent, oz);
                var grid = Grid.Create(doc, Line.CreateBound(start, end));
                grid.Name = (i + 1).ToString();
                createdGrids.Add(grid.Id.Value);
            }

            for (var j = 0; j < horizontalCount; j++)
            {
                var y = oy + j * horizontalSpacing;
                var start = new XYZ(ox, y, oz);
                var end = new XYZ(ox + extent, y, oz);
                var grid = Grid.Create(doc, Line.CreateBound(start, end));
                grid.Name = ((char)('A' + j)).ToString();
                createdGrids.Add(grid.Id.Value);
            }

            tx.Commit();
            return new { status = "Success", createdGridIds = createdGrids };
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw new McpException($"Failed to create grid system: {ex.Message}");
        }
    }

    [McpServerTool(Name = "revit_generate_levels", Title = "Generate Multiple Levels", ReadOnly = false)]
    [Description("Generates multiple levels from a list of level specifications.")]
    public static object GenerateLevels(
        [Description("Level specifications")] LevelSpec[] levels)
    {
        if (levels.Length == 0) throw new McpException("No level configurations provided.");

        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");

        var viewFamilyType = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType))
            .Cast<ViewFamilyType>().FirstOrDefault(vft => vft.ViewFamily == ViewFamily.FloorPlan);

        using var tx = new Transaction(doc, "Create Multiple Levels");
        tx.Start();
        try
        {
            var results = new List<object>();
            foreach (var levelInput in levels)
            {
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

                long? viewId = null;
                if (levelInput.CreateFloorPlanView && viewFamilyType is not null)
                {
                    var viewPlan = ViewPlan.Create(doc, viewFamilyType.Id, level.Id);
                    viewPlan.Name = $"Floor Plan - {level.Name}";
                    viewId = viewPlan.Id.Value;
                }

                results.Add(new { levelId = level.Id.Value, levelName = level.Name, viewId });
            }
            tx.Commit();
            return new { status = "Success", levels = results };
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw new McpException($"Failed to create levels: {ex.Message}");
        }
    }

    [McpServerTool(Name = "revit_shift_level_elevations", Title = "Shift Level Elevations", ReadOnly = false)]
    [Description("Adjusts the elevation values of existing levels by name.")]
    public static object ShiftLevelElevations(
        [Description("Level names and their new elevations")] Dictionary<string, double> levelElevations)
    {
        if (levelElevations.Count == 0) throw new McpException("No level elevations provided.");

        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");

        var allLevels = new FilteredElementCollector(doc).OfClass(typeof(Level))
            .Cast<Level>().ToList();

        using var tx = new Transaction(doc, "Adjust Level Elevations");
        tx.Start();
        try
        {
            var results = new List<object>();
            foreach (var value in levelElevations)
            {
                var levelName = value.Key;
                var elevation = value.Value;
                var level = allLevels.FirstOrDefault(l => l.Name.Equals(levelName, StringComparison.OrdinalIgnoreCase));
                if (level is null)
                {
                    results.Add(new { levelName, status = "NotFound" });
                    continue;
                }
                level.Elevation = elevation;
                results.Add(new { levelName, levelId = level.Id.Value, newElevation = elevation, status = "Updated" });
            }
            tx.Commit();
            return new { status = "Success", results = JsonSerializer.Serialize(results) };
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw new McpException($"Failed to adjust level elevations: {ex.Message}");
        }
    }
}
