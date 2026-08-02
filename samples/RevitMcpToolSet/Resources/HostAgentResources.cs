using System.ComponentModel;
using System.Text;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;

namespace RevitMcpToolSet.Resources;

/// <summary>
/// Host-agent resources migrated from <c>DevTools.Agents.Revit</c> (cheatsheets, live model context, warnings, version).
/// </summary>
[McpServerResourceType]
[Description("Reference and live-model context resources for agent workflows.")]
public static class HostAgentResources
{
    private const int MaxWarnings = 50;

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

    [McpServerResource(
        UriTemplate = "revit://csharp-cheatsheet",
        Name = "revit_csharp_cheatsheet",
        Title = "Revit C# Cheatsheet",
        MimeType = "text/markdown")]
    [Description("Common Revit C# API patterns. Read before writing execute_csharp_code.")]
    public static string GetCSharpCheatsheet() => EmbeddedContent.CSharpCheatsheet;

    [McpServerResource(
        UriTemplate = "revit://python-cheatsheet",
        Name = "revit_python_cheatsheet",
        Title = "Revit Python Cheatsheet",
        MimeType = "text/markdown")]
    [Description("Revit Python.NET patterns and PEP 723 deps. Read before writing execute_python_code.")]
    public static string GetPythonCheatsheet() => EmbeddedContent.PythonCheatsheet;

    [McpServerResource(
        UriTemplate = "revit://model/context",
        Name = "revit_model_context",
        Title = "Revit Model Context",
        MimeType = "text/markdown")]
    [Description("Live model snapshot: levels, categories, units, phases, active view.")]
    public static string GetModelContext()
    {
        var doc = RevitContext.ActiveDocument;
        if (doc is null)
            return "No document is currently open.";

        var sb = new StringBuilder();
        sb.AppendLine($"# Model: {doc.Title}");
        sb.AppendLine($"- Path: {doc.PathName}");
        sb.AppendLine();

        AppendUnits(sb, doc);
        AppendLevels(sb, doc);
        AppendPhases(sb, doc);
        AppendActiveView(sb);
        AppendCategorySummary(sb, doc);

        return sb.ToString();
    }

    [McpServerResource(
        UriTemplate = "revit://model/warnings",
        Name = "revit_model_warnings",
        Title = "Revit Model Warnings",
        MimeType = "text/markdown")]
    [Description("Active warnings in the current document.")]
    public static string GetModelWarnings()
    {
        var doc = RevitContext.ActiveDocument;
        if (doc is null)
            return "No document is currently open.";

        var warnings = doc.GetWarnings();
        if (warnings is null || warnings.Count == 0)
            return "# Warnings\n\nNo active warnings in the document.";

        var sb = new StringBuilder();
        sb.AppendLine($"# Warnings ({warnings.Count} total)");
        sb.AppendLine();

        var grouped = warnings
            .Take(MaxWarnings)
            .GroupBy(w => w.GetDescriptionText())
            .OrderByDescending(g => g.Count())
            .ToList();

        foreach (var group in grouped)
            AppendWarningGroup(sb, group);

        if (warnings.Count > MaxWarnings)
            sb.AppendLine($"*Showing first {MaxWarnings} of {warnings.Count} warnings.*");

        return sb.ToString();
    }

    [McpServerResource(
        UriTemplate = "revit://version",
        Name = "revit_version",
        Title = "Revit Version Info",
        MimeType = "text/markdown")]
    [Description("Host version, API version, runtime, and version-specific API notes.")]
    public static string GetVersionInfo()
    {
        var app = RevitContext.Application;
        var sb = new StringBuilder();

        sb.AppendLine("# Revit Version");
        sb.AppendLine($"- Product: {app.VersionName}");
        sb.AppendLine($"- Build: {app.VersionBuild}");
        sb.AppendLine($"- Number: {app.VersionNumber}");
        sb.AppendLine($"- Language: {app.Language}");
        sb.AppendLine();

        var versionNumber = app.VersionNumber ?? "";
        var versionYear = versionNumber.Length >= 4 && int.TryParse(versionNumber[..4], out var y) ? y : 0;
        var runtime = versionYear >= 2025 ? ".NET 8+ (net8.0-windows)" : ".NET Framework 4.8 (net48)";
        sb.AppendLine("## Runtime");
        sb.AppendLine($"- Framework: {runtime}");
        sb.AppendLine();

        sb.AppendLine("## API Version Notes");
        if (versionYear >= 2025)
        {
            sb.AppendLine("- Use `ElementId.Value` (long) — `IntegerValue` is obsolete");
            sb.AppendLine("- `ForgeTypeId` replaces `UnitType` and `DisplayUnitType`");
            sb.AppendLine("- `UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters)`");
        }
        else
        {
            sb.AppendLine("- Use `ElementId.IntegerValue` (int)");
            sb.AppendLine("- `UnitType` / `DisplayUnitType` enums still valid");
            sb.AppendLine("- `UnitUtils.ConvertToInternalUnits(value, DisplayUnitType.DUT_MILLIMETERS)`");
        }

        if (versionYear >= 2024)
        {
            sb.AppendLine("- `Document.GetWarnings()` available");
            sb.AppendLine("- `FailureMessage.GetFailureDefinitionId()` returns `FailureDefinitionId`");
        }

        if (versionYear >= 2022)
        {
            sb.AppendLine("- `FilteredElementCollector` supports `.GetElementCount()`");
            sb.AppendLine("- `Wall.Create` overload with all parameters available");
        }

        sb.AppendLine();
        return sb.ToString();
    }

    private static void AppendUnits(StringBuilder sb, Document doc)
    {
        sb.AppendLine("## Units");
        sb.AppendLine("- Internal: feet (always)");
        try
        {
            var formatOptions = doc.GetUnits().GetFormatOptions(SpecTypeId.Length);
            sb.AppendLine($"- Display: {formatOptions.GetUnitTypeId()}");
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
        if (view is null) return;

        sb.AppendLine("## Active View");
        sb.AppendLine($"- Name: {view.Name}");
        sb.AppendLine($"- Type: {view.ViewType}");
        sb.AppendLine($"- Id: {view.Id}");
        sb.AppendLine();
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

    private static void AppendWarningGroup(StringBuilder sb, IGrouping<string, FailureMessage> group)
    {
        var count = group.Count();
        sb.AppendLine($"## {group.Key} ({count})");
        foreach (var warning in group.Take(3))
        {
            var elements = warning.GetFailingElements();
            if (elements.Count <= 0) continue;
            var ids = string.Join(", ", elements.Select(id => id.ToString()));
            sb.AppendLine($"- Elements: [{ids}]");
        }
        if (count > 3)
            sb.AppendLine($"- ... and {count - 3} more");
        sb.AppendLine();
    }
}
