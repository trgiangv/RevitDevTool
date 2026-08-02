using System.Text;
using DevTools.Mcp.Catalog;
using ModelContextProtocol.Protocol;
using RevitDevTool.Core;

namespace DevTools.Agents.Revit.Resources;

/// <summary>
/// Live warnings from the active Revit document.
/// Helps agents understand existing conflicts before creating/modifying elements.
/// </summary>
public sealed class RevitModelWarnings : IBuiltInMcpResource
{
    private const int MaxWarnings = 50;

    public string UriTemplate => "revit://model/warnings";

    public Resource ProtocolResource { get; } = new()
    {
        Uri = "revit://model/warnings",
        Name = "Revit Model Warnings",
        Description = "Active warnings in the current document (duplicates, overlaps, constraints). Read to understand existing conflicts before modifications.",
        MimeType = "text/markdown"
    };

    public ReadResourceResult Read(string uri)
    {
        var doc = RevitContext.ActiveDocument;
        if (doc is null)
            return TextResult(uri, "No document is currently open.");

        var warnings = doc.GetWarnings();
        if (warnings == null || warnings.Count == 0)
            return TextResult(uri, "# Warnings\n\nNo active warnings in the document.");

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

        return TextResult(uri, sb.ToString());
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

    private static ReadResourceResult TextResult(string uri, string text) => new()
    {
        Contents = [new TextResourceContents { Uri = uri, MimeType = "text/markdown", Text = text }]
    };
}
