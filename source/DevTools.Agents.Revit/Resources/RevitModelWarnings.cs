using System.Text;
using System.ComponentModel;
using DevTools.Mcp.BuiltIn;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RevitDevTool.Core;

namespace DevTools.Agents.Revit.Resources;

/// <summary>
/// Live warnings from the active Revit document.
/// Helps agents understand existing conflicts before creating/modifying elements.
/// </summary>
public sealed class RevitModelWarnings : IBuiltInMcpResource
{
    private const int MaxWarnings = 50;

    public McpServerResource Primitive => McpServerResource.Create(typeof(RevitModelWarnings).GetMethod(nameof(ReadModelWarnings))!, this);

    [McpServerResource(UriTemplate = "revit://model/warnings", Name = "revit_model_warnings")]
    [Description("Active Revit document warnings.")]
    public ReadResourceResult ReadModelWarnings()
    {
        var doc = RevitContext.ActiveDocument;
        if (doc is null)
            return TextResult("revit://model/warnings", "No document is currently open.");

        var warnings = doc.GetWarnings();
        if (warnings == null || warnings.Count == 0)
            return TextResult("revit://model/warnings", "# Warnings\n\nNo active warnings in the document.");

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

        return TextResult("revit://model/warnings", sb.ToString());
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
