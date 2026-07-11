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
    [Description("Creates a duct segment in the model using the provided specifications.")]
    public static object PlaceDuct(
        [Description("Duct placement specifications")] DuctSpec spec)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var start = ToXyz(spec.Start);
        var end = ToXyz(spec.End);
        var length = start.DistanceTo(end);

        using var tx = new Transaction(doc, "MCP: revit_place_duct");
        tx.Start();
        try
        {
            var duct = Duct.Create(doc,
                spec.SystemTypeId.ToElementId(),
                spec.DuctTypeId.ToElementId(),
                spec.LevelId.ToElementId(),
                start, end);

            if (spec.Width > 0)
                duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM)?.Set(spec.Width);
            if (spec.Height > 0)
                duct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM)?.Set(spec.Height);
            if (spec.Diameter > 0)
                duct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM)?.Set(spec.Diameter);

            tx.Commit();
            return new { elementId = duct.Id.ToValue(), length };
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw new McpException($"Failed to place duct: {ex.Message}");
        }
    }

    [McpServerTool(Name = "revit_place_conduit", Title = "Place Conduit", ReadOnly = false)]
    [Description("Creates a conduit segment in the model using the provided specifications.")]
    public static object PlaceConduit(
        [Description("Conduit placement specifications")] ConduitSpec spec)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var start = ToXyz(spec.Start);
        var end = ToXyz(spec.End);
        var length = start.DistanceTo(end);

        using var tx = new Transaction(doc, "MCP: revit_place_conduit");
        tx.Start();
        try
        {
            var conduit = Conduit.Create(doc,
                spec.ConduitTypeId.ToElementId(),
                start, end,
                spec.LevelId.ToElementId());

            if (spec.Diameter > 0)
                conduit.get_Parameter(BuiltInParameter.RBS_CONDUIT_DIAMETER_PARAM)?.Set(spec.Diameter);

            tx.Commit();
            return new { elementId = conduit.Id.ToValue(), length };
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw new McpException($"Failed to place conduit: {ex.Message}");
        }
    }

    [McpServerTool(Name = "revit_place_pipe", Title = "Place Pipe", ReadOnly = false)]
    [Description("Creates a pipe segment in the model using the provided specifications.")]
    public static object PlacePipe(
        [Description("Pipe placement specifications")] PipeSpec spec)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var start = ToXyz(spec.Start);
        var end = ToXyz(spec.End);
        var length = start.DistanceTo(end);

        using var tx = new Transaction(doc, "MCP: revit_place_pipe");
        tx.Start();
        try
        {
            var pipe = Pipe.Create(doc,
                spec.SystemTypeId.ToElementId(),
                spec.PipeTypeId.ToElementId(),
                spec.LevelId.ToElementId(),
                start, end);

            if (spec.Diameter > 0)
                pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM)?.Set(spec.Diameter);

            tx.Commit();
            return new { elementId = pipe.Id.ToValue(), length };
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw new McpException($"Failed to place pipe: {ex.Message}");
        }
    }

    private static XYZ ToXyz(double[] point)
        => new(
            point.Length > 0 ? point[0] : 0,
            point.Length > 1 ? point[1] : 0,
            point.Length > 2 ? point[2] : 0);
}
