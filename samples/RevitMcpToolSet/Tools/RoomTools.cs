using System.ComponentModel;
using Autodesk.Revit.DB.Architecture;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Utilities;
namespace RevitMcpToolSet.Tools;

[McpServerToolType]
public static class RoomTools
{
    [McpServerTool(Name = "revit_list_rooms", Title = "List Rooms", ReadOnly = true)]
    [Description("Lists all placed rooms in the document with name, number, area, level, department, and location.")]
    public static object ListRooms()
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var rooms = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Rooms)
            .WhereElementIsNotElementType()
            .Cast<Room>()
            .Where(r => r.Area > 0)
            .Select(r =>
            {
                var location = r.Location as LocationPoint;
                var level = r.LevelId != ElementId.InvalidElementId
                    ? (doc.GetElement(r.LevelId) as Level)?.Name ?? ""
                    : "";
                var department = r.LookupParameter("Department")?.AsString()
                    ?? r.get_Parameter(BuiltInParameter.ROOM_DEPARTMENT)?.AsString()
                    ?? "";
                return new
                {
                    id = r.Id.ToValue(),
                    name = r.Name,
                    number = r.Number,
                    area = r.Area,
                    level,
                    department,
                    location = location is null
                        ? null
                        : new[] { location.Point.X, location.Point.Y, location.Point.Z },
                };
            })
            .ToList();

        return new { rooms };
    }
}
