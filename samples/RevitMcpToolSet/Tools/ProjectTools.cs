using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
namespace RevitMcpToolSet.Tools;

[McpServerToolType]
[Description("Tools for reading and writing project-level information and settings.")]
public static class ProjectTools
{
    [McpServerTool(Name = "revit_read_project_info", Title = "Read Project Info", ReadOnly = true)]
    [Description("Reads project information including name, number, address, and client name.")]
    public static object ReadProjectInfo()
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var info = doc.ProjectInformation;
        return new
        {
            projectInformation = JsonSerializer.Serialize(new
            {
                ProjectName = info.Name,
                ProjectNumber = info.Number,
                Address = info.Address,
                ClientName = info.ClientName,
            }),
        };
    }

    [McpServerTool(Name = "revit_read_project_units", Title = "Read Project Units", ReadOnly = true)]
    [Description("Reads the project unit settings for length, area, volume, and angle.")]
    public static object ReadProjectUnits()
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var units = doc.GetUnits();

        string GetUnitTypeId(ForgeTypeId specTypeId)
        {
            try { return units.GetFormatOptions(specTypeId).GetUnitTypeId().TypeId; }
            catch { return "unknown"; }
        }

        return new
        {
            projectUnits = JsonSerializer.Serialize(new
            {
                Length = GetUnitTypeId(SpecTypeId.Length),
                Area = GetUnitTypeId(SpecTypeId.Area),
                Volume = GetUnitTypeId(SpecTypeId.Volume),
                Angle = GetUnitTypeId(SpecTypeId.Angle),
            }),
        };
    }

    [McpServerTool(Name = "revit_write_project_info", Title = "Write Project Info", ReadOnly = false)]
    [Description("Writes project information fields such as name, number, address, and client.")]
    public static object WriteProjectInfo(
        [Description("Project name")] string projectName,
        [Description("Project number")] string projectNumber = "",
        [Description("Project address")] string projectAddress = "",
        [Description("Client name")] string clientName = "")
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        using var tx = new Transaction(doc, "Write Project Info");
        tx.Start();
        try
        {
            var info = doc.ProjectInformation;
            info.Name = projectName;
            if (!string.IsNullOrEmpty(projectNumber)) info.Number = projectNumber;
            if (!string.IsNullOrEmpty(projectAddress)) info.Address = projectAddress;
            if (!string.IsNullOrEmpty(clientName)) info.ClientName = clientName;
            tx.Commit();
            return new { status = "Success" };
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw new McpException($"Failed to write project info: {ex.Message}");
        }
    }

    [McpServerTool(Name = "revit_set_base_point", Title = "Set Project Base Point", ReadOnly = false)]
    [Description("Sets the project base point coordinates (East/West, North/South, Elevation).")]
    public static object SetBasePoint(
        [Description("East/West offset")] double eastWest,
        [Description("North/South offset")] double northSouth,
        [Description("Elevation")] double elevation)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var basePoint = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_ProjectBasePoint)
            .FirstElement() ?? throw new McpException("Project Base Point not found.");

        using var tx = new Transaction(doc, "Set Project Base Point");
        tx.Start();
        try
        {
            basePoint.get_Parameter(BuiltInParameter.BASEPOINT_EASTWEST_PARAM)?.Set(eastWest);
            basePoint.get_Parameter(BuiltInParameter.BASEPOINT_NORTHSOUTH_PARAM)?.Set(northSouth);
            basePoint.get_Parameter(BuiltInParameter.BASEPOINT_ELEVATION_PARAM)?.Set(elevation);
            tx.Commit();
            return new { status = "Success" };
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw new McpException($"Failed to set base point: {ex.Message}");
        }
    }

    [McpServerTool(Name = "revit_format_units", Title = "Format Value in Project Units", ReadOnly = true)]
    [Description("Formats a numeric value as a string using the project's unit settings for a given unit type.")]
    public static object FormatUnits(
        [Description("Numeric value to format")] double value,
        [Description("Unit type: Length, Area, Volume, or Angle")] string unitType)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var specTypeId = unitType.ToLowerInvariant() switch
        {
            "length" => SpecTypeId.Length,
            "area" => SpecTypeId.Area,
            "volume" => SpecTypeId.Volume,
            "angle" => SpecTypeId.Angle,
            _ => throw new McpException($"Unsupported unit type: '{unitType}'. Use Length, Area, Volume, or Angle."),
        };
        var formatted = UnitFormatUtils.Format(doc.GetUnits(), specTypeId, value, false);
        return new { formatted };
    }
}
