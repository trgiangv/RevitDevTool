using System.ComponentModel;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Data;
using RevitMcpToolSet.Utilities;
namespace RevitMcpToolSet.Tools;

[McpServerToolType]
[Description("Tools for placing MEP elements (ducts, conduits, pipes) in the Revit model.")]
public static class MepPlacementTools
{
    [McpServerTool(Name = "revit_place_duct", Title = "Place Duct", ReadOnly = false)]
    [Description("Places a duct segment in the model using the provided specifications.")]
    public static object PlaceDuct(
        [Description("Duct placement specifications")] DuctSpec info)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        using var tx = new Transaction(doc, "Place Duct");
        tx.Start();
        try
        {
            var start = new XYZ(info.StartX, info.StartY, info.StartZ);
            var end = new XYZ(info.EndX, info.EndY, info.EndZ);
            var duct = Duct.Create(doc,
                info.SystemTypeId.ToElementId(),
                info.DuctTypeId.ToElementId(),
                info.LevelId.ToElementId(),
                start, end);

            if (info.Width > 0)
                duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM)?.Set(info.Width);
            if (info.Height > 0)
                duct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM)?.Set(info.Height);

            tx.Commit();
            return new { ductId = duct.Id.ToValue() };
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw new McpException($"Failed to place duct: {ex.Message}");
        }
    }

    [McpServerTool(Name = "revit_place_conduit", Title = "Place Conduit", ReadOnly = false)]
    [Description("Places a conduit segment in the model using the provided specifications.")]
    public static object PlaceConduit(
        [Description("Conduit placement specifications")] ConduitSpec info)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        using var tx = new Transaction(doc, "Place Conduit");
        tx.Start();
        try
        {
            var start = new XYZ(info.StartX, info.StartY, info.StartZ);
            var end = new XYZ(info.EndX, info.EndY, info.EndZ);
            var conduit = Conduit.Create(doc,
                info.ConduitTypeId.ToElementId(),
                start, end,
                info.LevelId.ToElementId());

            if (info.Diameter > 0)
                conduit.get_Parameter(BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM)?.Set(info.Diameter);

            tx.Commit();
            return new { conduitId = conduit.Id.ToValue() };
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw new McpException($"Failed to place conduit: {ex.Message}");
        }
    }

    [McpServerTool(Name = "revit_place_pipe", Title = "Place Pipe", ReadOnly = false)]
    [Description("Places a pipe segment in the model using the provided specifications.")]
    public static object PlacePipe(
        [Description("Pipe placement specifications")] PipeSpec info)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        using var tx = new Transaction(doc, "Place Pipe");
        tx.Start();
        try
        {
            var start = new XYZ(info.StartX, info.StartY, info.StartZ);
            var end = new XYZ(info.EndX, info.EndY, info.EndZ);
            var pipe = Pipe.Create(doc,
                info.SystemTypeId.ToElementId(),
                info.PipeTypeId.ToElementId(),
                info.LevelId.ToElementId(),
                start, end);

            if (info.Diameter > 0)
                pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(info.Diameter);

            tx.Commit();
            return new { pipeId = pipe.Id.ToValue() };
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw new McpException($"Failed to place pipe: {ex.Message}");
        }
    }
}
