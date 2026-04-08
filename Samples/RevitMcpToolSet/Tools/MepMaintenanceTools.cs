using System.ComponentModel;
using Autodesk.Revit.DB.Mechanical;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Utilities;
namespace RevitMcpToolSet.Tools;

[McpServerToolType]
[Description("Tools for MEP system maintenance operations such as insulation and mark management.")]
public static class MepMaintenanceTools
{
    [McpServerTool(Name = "revit_insulate_duct_system", Title = "Insulate Duct System", ReadOnly = false)]
    [Description("Applies duct insulation to all ducts and fittings in a mechanical system.")]
    public static object InsulateDuctSystem(
        [Description("Mechanical system element ID")] long systemId,
        [Description("Insulation thickness in mm")] double thickness)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var system = doc.GetElement(systemId.ToElementId()) as MechanicalSystem
            ?? throw new McpException($"Mechanical system {systemId} not found.");

        var insulationTypeId = new FilteredElementCollector(doc).OfClass(typeof(DuctInsulationType))
            .FirstElementId();
        if (insulationTypeId == ElementId.InvalidElementId)
            throw new McpException("No duct insulation type found in the document.");

        var thicknessInFeet = thickness / 304.8;
        var insulated = 0;

        using var tx = new Transaction(doc, "Insulate Duct System");
        tx.Start();
        try
        {
            foreach (Element element in system.DuctNetwork)
            {
                try
                {
                    if (element is Duct duct)
                    {
                        DuctInsulation.Create(doc, duct.Id, insulationTypeId, thicknessInFeet);
                        insulated++;
                    }
                    else if (element.Category?.Id.ToValue() == (long)BuiltInCategory.OST_DuctFitting)
                    {
                        DuctInsulation.Create(doc, element.Id, insulationTypeId, thicknessInFeet);
                        insulated++;
                    }
                }
                catch { /* skip elements that can't be insulated */ }
            }
            tx.Commit();
            return new { outcome = $"Applied insulation to {insulated} duct elements." };
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw new McpException($"Failed to insulate duct system: {ex.Message}");
        }
    }

    [McpServerTool(Name = "revit_clear_mep_marks_bulk", Title = "Clear MEP Marks in Bulk", ReadOnly = false)]
    [Description("Clears the Mark parameter on MEP elements, optionally filtered by category name.")]
    public static object ClearMepMarksBulk(
        [Description("Category names to filter (optional)")] string[]? mepCategories = null,
        [Description("If true, clears marks on all MEP elements regardless of category")] bool clearAll = false)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        var collector = new FilteredElementCollector(doc).WhereElementIsNotElementType();

        if (!clearAll && mepCategories is { Length: > 0 })
        {
            var catIds = new List<ElementId>();
            foreach (var catName in mepCategories)
            {
                foreach (Category cat in doc.Settings.Categories)
                {
                    if (cat.Name.Equals(catName, StringComparison.OrdinalIgnoreCase))
                    {
                        catIds.Add(cat.Id);
                        break;
                    }
                }
            }
            if (catIds.Count > 0)
                collector = collector.WherePasses(new ElementMulticategoryFilter(catIds));
        }

        var elements = collector.ToList()
            .Where(e => e.get_Parameter(BuiltInParameter.ALL_MODEL_MARK) is not null)
            .ToList();

        using var tx = new Transaction(doc, "Clear MEP Marks Bulk");
        tx.Start();
        var cleared = 0;
        foreach (var element in elements)
        {
            var markParam = element.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
            if (markParam is not null && !markParam.IsReadOnly)
            {
                markParam.Set("");
                cleared++;
            }
        }
        tx.Commit();
        return new { status = "Success", clearedCount = cleared };
    }

    [McpServerTool(Name = "revit_clear_mep_marks", Title = "Clear MEP Marks", ReadOnly = false)]
    [Description("Clears the Mark parameter on specific MEP elements by their IDs.")]
    public static object ClearMepMarks(
        [Description("Element IDs to clear marks on")] long[] elementIds)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        if (elementIds.Length == 0) throw new McpException("No element IDs provided.");

        using var tx = new Transaction(doc, "Clear MEP Marks");
        tx.Start();
        var cleared = 0;
        foreach (var eid in elementIds)
        {
            var element = doc.GetElement(eid.ToElementId());
            if (element is null) continue;
            var markParam = element.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
            if (markParam is not null && !markParam.IsReadOnly)
            {
                markParam.Set("");
                cleared++;
            }
        }
        tx.Commit();
        return new { status = "Success", clearedCount = cleared };
    }
}
