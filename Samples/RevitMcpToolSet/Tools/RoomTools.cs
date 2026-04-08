using System.ComponentModel;
using System.Text.Json;
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
    [Description("Lists all placed rooms in the document with their area, level, and location.")]
    public static object ListRooms()
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var rooms = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Rooms)
            .Cast<Room>()
            .Where(r => r.Area > 0)
            .Select(r =>
            {
                var location = r.Location as LocationPoint;
                var level = r.LevelId != ElementId.InvalidElementId
                    ? (doc.GetElement(r.LevelId) as Level)?.Name ?? ""
                    : "";
                return new
                {
                    Id = r.Id.ToValue(),
                    Name = r.Name,
                    Number = r.Number,
                    Area = r.Area,
                    Level = level,
                    LocationX = location?.Point.X,
                    LocationY = location?.Point.Y,
                };
            })
            .ToList();

        return new { rooms = JsonSerializer.Serialize(rooms) };
    }

    [McpServerTool(Name = "revit_override_room_color", Title = "Override Room Color", ReadOnly = false)]
    [Description("Applies a solid color graphic override to a room in the active view.")]
    public static object OverrideRoomColor(
        [Description("Room element ID")] long roomId,
        [Description("Color as [R, G, B] bytes")] int[] color)
    {
        if (color.Length < 3) throw new McpException("Color must have 3 components [R, G, B].");

        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var room = doc.GetElement(roomId.ToElementId()) as Room
            ?? throw new McpException($"Room {roomId} not found.");
        var activeView = RevitContext.ActiveView
            ?? throw new McpException("No active view.");

        var revitColor = new Color((byte)color[0], (byte)color[1], (byte)color[2]);

        var solidFillPatternId = new FilteredElementCollector(doc)
            .OfClass(typeof(FillPatternElement))
            .Cast<FillPatternElement>()
            .FirstOrDefault(fp => fp.GetFillPattern().IsSolidFill)?.Id ?? ElementId.InvalidElementId;

        using var tx = new Transaction(doc, "Color Room");
        tx.Start();
        try
        {
            var overrideSettings = activeView.GetElementOverrides(room.Id);
            overrideSettings.SetSurfaceForegroundPatternColor(revitColor);
            overrideSettings.SetSurfaceForegroundPatternVisible(true);
            if (solidFillPatternId != ElementId.InvalidElementId)
                overrideSettings.SetSurfaceForegroundPatternId(solidFillPatternId);
            activeView.SetElementOverrides(room.Id, overrideSettings);
            tx.Commit();
            return new { status = "Success" };
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw new McpException($"Failed to color room: {ex.Message}");
        }
    }

    [McpServerTool(Name = "revit_rename_room", Title = "Rename Room", ReadOnly = false)]
    [Description("Renames a room by its element ID.")]
    public static object RenameRoom(
        [Description("Room element ID")] long roomId,
        [Description("New room name")] string roomName)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var room = doc.GetElement(roomId.ToElementId()) as Room
                                  ?? throw new McpException($"Room {roomId} not found.");

        using var tx = new Transaction(doc, "Set Room Name");
        tx.Start();
        room.Name = roomName;
        tx.Commit();
        return new { status = "Success" };
    }
}
