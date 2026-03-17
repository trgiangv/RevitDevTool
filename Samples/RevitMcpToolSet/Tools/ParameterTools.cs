using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Data;
using RevitMcpToolSet.Utilities;
namespace RevitMcpToolSet.Tools;

[McpServerToolType]
[Description("Tools for reading and writing element parameters in Revit.")]
[PublicAPI]
public static class ParameterTools
{
    [McpServerTool(Name = "revit_read_parameters", Title = "Read Element Parameters", ReadOnly = true)]
    [Description("Reads all parameters for a given element and returns their names, values, and metadata.")]
    public static object ReadParameters(
        [Description("Element ID")] long elementId)
    {
        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");
        var element = doc.GetElement(elementId.ToElementId())
            ?? throw new McpException($"Element with ID {elementId} not found.");

        var parameters = new List<ParameterEntry>();
        foreach (Parameter param in element.Parameters)
        {
            parameters.Add(new ParameterEntry
            {
                Name = param.Definition.Name,
                Value = ParameterAccessor.GetParameterValue(param),
                StorageType = param.StorageType.ToString(),
                IsReadOnly = param.IsReadOnly,
                IsShared = param.IsShared,
                HasValue = param.HasValue,
                BuiltInParam = ParameterAccessor.GetBuiltInParam(param),
            });
        }
        return new { outcome = "Success", instanceParameters = parameters };
    }

    [McpServerTool(Name = "revit_write_parameters", Title = "Write Element Parameters", ReadOnly = false)]
    [Description("Writes parameter values to one or more elements.")]
    public static object WriteParameters(
        [Description("Array of element IDs")] long[] elementIds,
        [Description("Parameter updates to apply")] ParameterUpdate[] updates)
    {
        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");
        if (elementIds.Length == 0) throw new McpException("No element IDs provided.");
        if (updates.Length == 0) throw new McpException("No parameter updates provided.");

        var outcome = new OperationOutcome();
        using var tx = new Transaction(doc, "Write Element Parameters");
        tx.Start();
        foreach (var eid in elementIds)
        {
            var element = doc.GetElement(eid.ToElementId());
            if (element is null) { outcome.Record(false, $"Element {eid} not found", eid); continue; }

            foreach (var update in updates)
            {
                var (success, message) = ParameterAccessor.SetParameterValue(element, update.ParameterName, update.Value);
                outcome.Record(success, message, eid);
            }
        }
        tx.Commit();
        return outcome.Summarize();
    }

    [McpServerTool(Name = "revit_clone_parameters", Title = "Clone Parameters Between Elements", ReadOnly = false)]
    [Description("Copies parameter values from a source element to one or more target elements.")]
    public static object CloneParameters(
        [Description("Source element ID")] long sourceElementId,
        [Description("Target element IDs")] long[] targetElementIds,
        [Description("Parameter names to copy")] string[] parameterNames)
    {
        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");
        var source = doc.GetElement(sourceElementId.ToElementId())
            ?? throw new McpException($"Source element {sourceElementId} not found.");

        var sourceValues = new Dictionary<string, string>();
        foreach (var name in parameterNames)
        {
            var param = source.GetParameters(name).FirstOrDefault();
            if (param is not null)
                sourceValues[name] = ParameterAccessor.GetParameterValue(param);
        }

        var outcome = new OperationOutcome();
        using var tx = new Transaction(doc, "Clone Parameters");
        tx.Start();
        foreach (var eid in targetElementIds)
        {
            var target = doc.GetElement(eid.ToElementId());
            if (target is null) { outcome.Record(false, $"Element {eid} not found", eid); continue; }
            foreach (var pair in sourceValues)
            {
                var (success, message) = ParameterAccessor.SetParameterValue(target, pair.Key, pair.Value);
                outcome.Record(success, message, eid);
            }
        }
        tx.Commit();
        return outcome.Summarize();
    }

    [McpServerTool(Name = "revit_swap_element_type", Title = "Swap Element Type", ReadOnly = false)]
    [Description("Changes the family type of one or more elements to a different type.")]
    public static object SwapElementType(
        [Description("Element IDs to change")] long[] elementIds,
        [Description("New type element ID")] long newTypeId)
    {
        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");
        if (elementIds.Length == 0) throw new McpException("No element IDs provided.");

        var outcome = new OperationOutcome();
        using var tx = new Transaction(doc, "Swap Element Type");
        tx.Start();
        foreach (var eid in elementIds)
        {
            var element = doc.GetElement(eid.ToElementId());
            if (element is null) { outcome.Record(false, $"Element {eid} not found", eid); continue; }
            var (success, message, _) = ParameterAccessor.ChangeType(element, newTypeId);
            outcome.Record(success, message, eid);
        }
        tx.Commit();
        return new { outcome = "Success", newTypeId, results = outcome.Summarize() };
    }
}
