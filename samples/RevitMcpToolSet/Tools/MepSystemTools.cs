using System.ComponentModel;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Utilities;

namespace RevitMcpToolSet.Tools;

[McpServerToolType]
[Description("Tools for querying and maintaining MEP systems and circuits in the Revit model.")]
public static class MepSystemTools
{
    [McpServerTool(Name = "revit_list_mep_systems", Title = "List MEP Systems", ReadOnly = true)]
    [Description("Enumerates MEP systems or electrical circuits in the document.")]
    public static object ListMepSystems(
        [Description("System kind: duct, pipe, electrical, or all")] string kind = "all")
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var normalizedKind = kind.Trim().ToLowerInvariant();

        var systems = normalizedKind switch
        {
            "duct" => ListDuctSystems(doc),
            "pipe" => ListPipeSystems(doc),
            "electrical" => ListElectricalSystems(doc),
            "all" => ListDuctSystems(doc)
                .Concat(ListPipeSystems(doc))
                .Concat(ListElectricalSystems(doc))
                .ToList(),
            _ => throw new McpException($"Invalid kind '{kind}'. Expected duct, pipe, electrical, or all."),
        };

        return new { systems };
    }

    [McpServerTool(Name = "revit_insulate_duct_system", Title = "Insulate Duct System", ReadOnly = false)]
    [Description("Applies duct insulation to all ducts and fittings in a mechanical system.")]
    public static object InsulateDuctSystem(
        [Description("Mechanical system element ID")] long systemId,
        [Description("Insulation thickness in millimeters")] double thickness_mm)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var system = doc.GetElement(systemId.ToElementId()) as MechanicalSystem
            ?? throw new McpException($"Mechanical system {systemId} not found.");

        var insulationTypeId = new FilteredElementCollector(doc).OfClass(typeof(DuctInsulationType))
            .FirstElementId();
        if (insulationTypeId == ElementId.InvalidElementId)
            throw new McpException("No duct insulation type found in the document.");

        var thicknessInFeet = thickness_mm / 304.8;
        var insulatedCount = 0;

        using var tx = new Transaction(doc, "MCP: revit_insulate_duct_system");
        tx.Start();
        try
        {
            foreach (Element element in system.DuctNetwork)
            {
                try
                {
                    if (element is Duct || element.Category?.Id.ToValue() == (long)BuiltInCategory.OST_DuctFitting)
                    {
                        DuctInsulation.Create(doc, element.Id, insulationTypeId, thicknessInFeet);
                        insulatedCount++;
                    }
                }
                catch
                {
                    // Skip elements that cannot be insulated.
                }
            }

            tx.Commit();
            return new { insulated_count = insulatedCount };
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw new McpException($"Failed to insulate duct system: {ex.Message}");
        }
    }

    private static List<object> ListDuctSystems(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(MechanicalSystem))
            .Cast<MechanicalSystem>()
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => MapSystem(s.Id.ToValue(), s.Name, s.SystemType, s.DuctNetwork?.Size ?? 0))
            .ToList();
    }

    private static List<object> ListPipeSystems(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(PipingSystem))
            .Cast<PipingSystem>()
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => MapSystem(s.Id.ToValue(), s.Name, s.SystemType, s.PipingNetwork?.Size ?? 0))
            .ToList();
    }

    private static List<object> ListElectricalSystems(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(ElectricalSystem))
            .Cast<ElectricalSystem>()
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => MapSystem(
                s.Id.ToValue(),
                s.Name,
                s.SystemType,
                s.Elements?.Size ?? 0,
                s.CircuitType.ToString()))
            .ToList();
    }

    private static object MapSystem(long id, string name, Enum systemType, int elementCount, string? classificationOverride = null)
    {
        var typeLabel = FormatEnumLabel(systemType);
        return new
        {
            id,
            name,
            type = typeLabel,
            element_count = elementCount,
            classification = classificationOverride ?? GetClassification(typeLabel),
        };
    }

    private static string FormatEnumLabel(Enum value)
    {
        var name = value.ToString();
        if (name == "UndefinedSystemType")
            return "Undefined";

        return string.Concat(name.Select((c, i) =>
            i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]) ? " " + c : c.ToString()));
    }

    private static string GetClassification(string typeLabel)
    {
        var spaceIndex = typeLabel.IndexOf(' ');
        return spaceIndex > 0 ? typeLabel[..spaceIndex] : typeLabel;
    }
}
