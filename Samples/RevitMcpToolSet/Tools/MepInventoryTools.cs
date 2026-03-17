using System.ComponentModel;
using System.Text.Json;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Utilities;
namespace RevitMcpToolSet.Tools;

[McpServerToolType]
[Description("Tools for inventorying MEP system types, systems, and elements in the Revit model.")]
public static class MepInventoryTools
{
    [McpServerTool(Name = "revit_list_duct_types", Title = "List Duct Types", ReadOnly = true)]
    [Description("Lists all duct types available in the document.")]
    public static object ListDuctTypes()
    {
        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");
        var ductTypes = new FilteredElementCollector(doc).OfClass(typeof(DuctType))
            .Cast<DuctType>().Select(t => new { Id = t.Id.ToValue(), Name = t.Name }).ToList();
        return new { ductTypes = JsonSerializer.Serialize(ductTypes) };
    }

    [McpServerTool(Name = "revit_list_duct_system_types", Title = "List Duct System Types", ReadOnly = true)]
    [Description("Lists all duct system types available in the document.")]
    public static object ListDuctSystemTypes()
    {
        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");
        var ductSystemTypes = new FilteredElementCollector(doc).OfClass(typeof(MechanicalSystemType))
            .Cast<MechanicalSystemType>().Select(t => new { Id = t.Id.ToValue(), Name = t.Name }).ToList();
        return new { ductSystemTypes = JsonSerializer.Serialize(ductSystemTypes) };
    }

    [McpServerTool(Name = "revit_list_duct_systems", Title = "List Duct Systems", ReadOnly = true)]
    [Description("Lists all mechanical duct systems in the document.")]
    public static object ListDuctSystems()
    {
        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");
        var ductSystems = new FilteredElementCollector(doc).OfClass(typeof(MechanicalSystem))
            .Cast<MechanicalSystem>().Select(s => new { Id = s.Id.ToValue(), Name = s.Name }).ToList();
        return new { ductSystems = JsonSerializer.Serialize(ductSystems) };
    }

    [McpServerTool(Name = "revit_list_ducts_in_system", Title = "List Ducts in System", ReadOnly = true)]
    [Description("Lists all duct elements belonging to a specific mechanical system.")]
    public static object ListDuctsInSystem(
        [Description("Mechanical system element ID")] long systemId)
    {
        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");
        var system = doc.GetElement(systemId.ToElementId()) as MechanicalSystem
            ?? throw new McpException($"Mechanical system {systemId} not found.");

        var ducts = system.DuctNetwork
            .Cast<Element>()
            .Select(e => new { Id = e.Id.ToValue(), Name = e.Name ?? "" })
            .ToList();
        return new { ducts = JsonSerializer.Serialize(ducts) };
    }

    [McpServerTool(Name = "revit_list_pipe_types", Title = "List Pipe Types", ReadOnly = true)]
    [Description("Lists all pipe types available in the document.")]
    public static object ListPipeTypes()
    {
        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");
        var pipeTypes = new FilteredElementCollector(doc).OfClass(typeof(PipeType))
            .Cast<PipeType>().Select(t => new { Id = t.Id.ToValue(), Name = t.Name }).ToList();
        return new { pipeTypes = JsonSerializer.Serialize(pipeTypes) };
    }

    [McpServerTool(Name = "revit_list_conduit_types", Title = "List Conduit Types", ReadOnly = true)]
    [Description("Lists all conduit types available in the document.")]
    public static object ListConduitTypes()
    {
        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");
        var conduitTypes = new FilteredElementCollector(doc).OfClass(typeof(ConduitType))
            .Cast<ConduitType>().Select(t => new { Id = t.Id.ToValue(), Name = t.Name }).ToList();
        return new { conduitTypes = JsonSerializer.Serialize(conduitTypes) };
    }

    [McpServerTool(Name = "revit_list_wire_types", Title = "List Wire Types", ReadOnly = true)]
    [Description("Lists all wire types available in the document.")]
    public static object ListWireTypes()
    {
        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");
        var wireTypes = new FilteredElementCollector(doc).OfClass(typeof(WireType))
            .Cast<WireType>().Select(t => new { Id = t.Id.ToValue(), Name = t.Name }).ToList();
        return new { wireTypes = JsonSerializer.Serialize(wireTypes) };
    }

    [McpServerTool(Name = "revit_list_circuits", Title = "List Electrical Circuits", ReadOnly = true)]
    [Description("Lists all electrical circuits in the document.")]
    public static object ListCircuits()
    {
        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");
        var circuits = new FilteredElementCollector(doc).OfClass(typeof(ElectricalSystem))
            .Cast<ElectricalSystem>().Select(c => new { Id = c.Id.ToValue(), Name = c.Name }).ToList();
        return new { circuits = JsonSerializer.Serialize(circuits) };
    }

    [McpServerTool(Name = "revit_list_mep_marked", Title = "List MEP Elements with Marks", ReadOnly = true)]
    [Description("Lists MEP elements that have a non-empty Mark parameter value, optionally filtered by category.")]
    public static object ListMepMarked(
        [Description("Optional category names to filter (e.g. ['Ducts', 'Pipes'])")] string[]? mepCategories = null)
    {
        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");

        var collector = new FilteredElementCollector(doc).WhereElementIsNotElementType();

        if (mepCategories is { Length: > 0 })
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
            .Where(e =>
            {
                var markParam = e.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                return markParam is not null && !string.IsNullOrEmpty(markParam.AsString());
            })
            .Select(e => new
            {
                Id = e.Id.ToValue(),
                Category = e.Category?.Name ?? "",
                Mark = e.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString() ?? "",
            })
            .ToList();

        return new { elements = JsonSerializer.Serialize(elements) };
    }

    [McpServerTool(Name = "revit_generate_panel_schedule", Title = "Generate Panel Schedule", ReadOnly = true)]
    [Description("Validates that a panel element exists and is ready for panel schedule generation.")]
    public static object GeneratePanelSchedule(
        [Description("Panel element ID")] long panelId)
    {
        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");
        var panel = doc.GetElement(panelId.ToElementId())
            ?? throw new McpException($"Panel element {panelId} not found.");
        return new { status = "Validated", panelId = panel.Id.ToValue(), panelName = panel.Name };
    }
}
