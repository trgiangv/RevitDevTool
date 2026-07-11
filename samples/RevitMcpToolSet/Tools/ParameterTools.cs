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
    [McpServerTool(Name = "revit_write_parameters", Title = "Write Element Parameters", ReadOnly = false)]
    [Description("Writes parameter values to one or more elements.")]
    public static object WriteParameters(
        [Description("Array of element IDs")] long[] elementIds,
        [Description("Parameter updates to apply")] ParameterUpdate[] updates)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        if (elementIds.Length == 0) throw new McpException("No element IDs provided.");
        if (updates.Length == 0) throw new McpException("No parameter updates provided.");

        var outcome = new OperationOutcome();
        using var tx = new Transaction(doc, "MCP: revit_write_parameters");
        tx.Start();
        foreach (var eid in elementIds)
        {
            var element = doc.GetElement(eid.ToElementId());
            if (element is null)
            {
                outcome.RecordFailure(eid, $"Element {eid} not found");
                continue;
            }

            foreach (var update in updates)
            {
                try
                {
                    var (success, message) = ParameterAccessor.SetParameterValue(
                        element, update.ParameterName, update.Value);
                    outcome.Record(success, message, eid);
                }
                catch (Exception ex)
                {
                    outcome.RecordFailure(eid, ex);
                }
            }
        }
        tx.Commit();
        return outcome.Summarize();
    }

    [McpServerTool(Name = "revit_clone_parameters", Title = "Clone Parameters Between Elements", ReadOnly = false)]
    [Description("Copies parameter values from a source element to one or more target elements.")]
    public static object CloneParameters(
        [Description("Source element ID")] long sourceId,
        [Description("Target element IDs")] long[] targetIds,
        [Description("Parameter names to copy")] string[] paramNames)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        if (targetIds.Length == 0) throw new McpException("No target element IDs provided.");
        if (paramNames.Length == 0) throw new McpException("No parameter names provided.");

        var source = doc.GetElement(sourceId.ToElementId())
            ?? throw new McpException($"Source element {sourceId} not found.");

        var sourceValues = new Dictionary<string, string>();
        var skipped = new List<object>();
        foreach (var name in paramNames)
        {
            var param = source.GetParameters(name).FirstOrDefault();
            if (param is null)
            {
                skipped.Add(new { paramName = name, reason = "Parameter not found on source element" });
                continue;
            }
            sourceValues[name] = ParameterAccessor.GetParameterValue(param);
        }

        var successCount = 0;
        using var tx = new Transaction(doc, "MCP: revit_clone_parameters");
        tx.Start();
        foreach (var eid in targetIds)
        {
            var target = doc.GetElement(eid.ToElementId());
            if (target is null)
            {
                skipped.Add(new { elementId = eid, reason = "Target element not found" });
                continue;
            }

            foreach (var pair in sourceValues)
            {
                try
                {
                    var (success, message) = ParameterAccessor.SetParameterValue(target, pair.Key, pair.Value);
                    if (success)
                        successCount++;
                    else
                        skipped.Add(new { elementId = eid, paramName = pair.Key, reason = message });
                }
                catch (Exception ex)
                {
                    skipped.Add(new { elementId = eid, paramName = pair.Key, reason = ex.Message });
                }
            }
        }
        tx.Commit();
        return new { success_count = successCount, skipped };
    }

    [McpServerTool(Name = "revit_swap_type", Title = "Swap Element Type", ReadOnly = false)]
    [Description("Changes the family type of one or more elements to a different type.")]
    public static object SwapType(
        [Description("Element IDs to change")] long[] elementIds,
        [Description("New type element ID")] long newTypeId)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        if (elementIds.Length == 0) throw new McpException("No element IDs provided.");

        var newType = doc.GetElement(newTypeId.ToElementId());
        if (newType is null)
            throw new McpException($"Type element {newTypeId} not found.");

        var outcome = new OperationOutcome();
        using var tx = new Transaction(doc, "MCP: revit_swap_type");
        tx.Start();
        foreach (var eid in elementIds)
        {
            try
            {
                var element = doc.GetElement(eid.ToElementId());
                if (element is null)
                {
                    outcome.RecordFailure(eid, $"Element {eid} not found");
                    continue;
                }

                var (success, message, _) = ParameterAccessor.ChangeType(element, newTypeId);
                if (success)
                    outcome.RecordSuccess();
                else
                    outcome.RecordFailure(eid, message);
            }
            catch (Exception ex)
            {
                outcome.RecordFailure(eid, ex);
            }
        }
        tx.Commit();
        return new
        {
            swapped_count = outcome.SuccessCount,
            failures = outcome.Failures.Count > 0 ? outcome.Failures : null,
        };
    }
}
