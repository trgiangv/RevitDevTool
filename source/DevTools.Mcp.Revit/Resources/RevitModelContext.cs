using System.Text;
using DevTools.Mcp.Catalog;
using ModelContextProtocol.Protocol;
using RevitDevTool.Core;

namespace DevTools.Mcp.Revit.Resources;

/// <summary>
/// Live snapshot of the active Revit model: levels, categories, element counts, units, phases.
/// Allows AI agents to understand model state before writing code.
/// </summary>
public sealed class RevitModelContext : IBuiltInMcpResource
{
    private static readonly (string Name, BuiltInCategory Cat)[] TrackedCategories =
    [
        ("Walls", BuiltInCategory.OST_Walls),
        ("Floors", BuiltInCategory.OST_Floors),
        ("Roofs", BuiltInCategory.OST_Roofs),
        ("Columns", BuiltInCategory.OST_Columns),
        ("Structural Framing", BuiltInCategory.OST_StructuralFraming),
        ("Doors", BuiltInCategory.OST_Doors),
        ("Windows", BuiltInCategory.OST_Windows),
        ("Rooms", BuiltInCategory.OST_Rooms),
        ("Furniture", BuiltInCategory.OST_Furniture),
        ("Generic Models", BuiltInCategory.OST_GenericModel),
        ("Ducts", BuiltInCategory.OST_DuctCurves),
        ("Pipes", BuiltInCategory.OST_PipeCurves),
    ];

    public string UriTemplate => "revit://model/context";

    public Resource ProtocolResource { get; } = new()
    {
        Uri = "revit://model/context",
        Name = "Revit Model Context",
        Description = "Live model snapshot: levels, categories with element counts, units, phases, active view. Read before writing code to avoid guessing.",
        MimeType = "text/markdown"
    };

    public ReadResourceResult Read(string uri)
    {
        var doc = RevitContext.ActiveDocument;
        if (doc is null)
        {
            return TextResult(uri, "No document is currently open.");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# Model: {doc.Title}");
        sb.AppendLine($"- Path: {doc.PathName}");
        sb.AppendLine();

        AppendUnits(sb, doc);
        AppendLevels(sb, doc);
        AppendPhases(sb, doc);
        AppendActiveView(sb);
        AppendCategorySummary(sb, doc);

        return TextResult(uri, sb.ToString());
    }

    private static void AppendUnits(StringBuilder sb, Document doc)
    {
        sb.AppendLine("## Units");
        sb.AppendLine("- Internal: feet (always)");
        try
        {
            var formatOptions = doc.GetUnits().GetFormatOptions(SpecTypeId.Length);
            var unitTypeId = formatOptions.GetUnitTypeId();
            sb.AppendLine($"- Display: {unitTypeId}");
        }
        catch
        {
            sb.AppendLine("- Display: (unable to read)");
        }
        sb.AppendLine();
    }

    private static void AppendLevels(StringBuilder sb, Document doc)
    {
        var levels = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(l => l.Elevation)
            .ToList();

        sb.AppendLine($"## Levels ({levels.Count})");
        foreach (var level in levels)
        {
            var elevationMm = level.Elevation * 304.8;
            sb.AppendLine($"- {level.Name}: {elevationMm:F0} mm (Id: {level.Id})");
        }
        sb.AppendLine();
    }

    private static void AppendPhases(StringBuilder sb, Document doc)
    {
        var phases = new FilteredElementCollector(doc)
            .OfClass(typeof(Phase))
            .Cast<Phase>()
            .ToList();

        if (phases.Count <= 0) return;
        sb.AppendLine($"## Phases ({phases.Count})");
        foreach (var phase in phases)
            sb.AppendLine($"- {phase.Name} (Id: {phase.Id})");
        sb.AppendLine();
    }

    private static void AppendActiveView(StringBuilder sb)
    {
        var view = RevitContext.ActiveView;
        if (view is not null)
        {
            sb.AppendLine("## Active View");
            sb.AppendLine($"- Name: {view.Name}");
            sb.AppendLine($"- Type: {view.ViewType}");
            sb.AppendLine($"- Id: {view.Id}");
            sb.AppendLine();
        }
    }

    private static void AppendCategorySummary(StringBuilder sb, Document doc)
    {
        sb.AppendLine("## Element Counts");
        foreach (var (name, cat) in TrackedCategories)
        {
            var count = new FilteredElementCollector(doc)
                .OfCategory(cat)
                .WhereElementIsNotElementType()
                .GetElementCount();
            if (count > 0)
                sb.AppendLine($"- {name}: {count}");
        }
        sb.AppendLine();
    }

    private static ReadResourceResult TextResult(string uri, string text) => new()
    {
        Contents = [new TextResourceContents { Uri = uri, MimeType = "text/markdown", Text = text }]
    };
}
