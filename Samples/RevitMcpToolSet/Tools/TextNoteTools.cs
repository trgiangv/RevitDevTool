using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Utilities;
namespace RevitMcpToolSet.Tools;

[McpServerToolType]
public static class TextNoteTools
{
    [McpServerTool(Name = "revit_list_text_notes", Title = "List Text Notes", ReadOnly = true)]
    [Description("Lists all text notes in the document, optionally filtered by view name or empty content.")]
    public static object ListTextNotes(
        [Description("Filter by view name (optional)")] string? viewName = null,
        [Description("If true, excludes text notes with empty content")] bool excludeEmpty = false)
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        IEnumerable<TextNote> textNotes;

        if (!string.IsNullOrEmpty(viewName))
        {
            var view = new FilteredElementCollector(doc).OfClass(typeof(View))
                .Cast<View>().FirstOrDefault(v => !v.IsTemplate && v.Name.Equals(viewName, StringComparison.OrdinalIgnoreCase))
                ?? throw new McpException($"View '{viewName}' not found.");
            textNotes = new FilteredElementCollector(doc, view.Id).OfClass(typeof(TextNote)).Cast<TextNote>();
        }
        else
        {
            textNotes = new FilteredElementCollector(doc).OfClass(typeof(TextNote)).Cast<TextNote>();
        }

        if (excludeEmpty)
            textNotes = textNotes.Where(tn => !string.IsNullOrWhiteSpace(tn.Text));

        var results = textNotes.Select(tn => new
        {
            Id = tn.Id.ToValue(),
            Text = tn.Text,
            ViewId = tn.OwnerViewId.ToValue(),
        }).ToList();

        return new { textNotes = JsonSerializer.Serialize(results) };
    }

    [McpServerTool(Name = "revit_list_view_text_notes", Title = "List Active View Text Notes", ReadOnly = true)]
    [Description("Lists all text notes in the currently active view.")]
    public static object ListViewTextNotes()
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var activeView = RevitContext.ActiveView ?? throw new McpException("No active view.");

        var textNotes = new FilteredElementCollector(doc, activeView.Id)
            .OfClass(typeof(TextNote))
            .Cast<TextNote>()
            .Select(tn => new { Id = tn.Id.ToValue(), Text = tn.Text })
            .ToList();

        return new { textNotes = JsonSerializer.Serialize(textNotes) };
    }

    [McpServerTool(Name = "revit_change_text_case", Title = "Change Text Note Case", ReadOnly = false)]
    [Description("Changes the capitalization of text notes. Scope: 'current_view', 'all', or a specific view name. Style: 'UPPER', 'lower', 'Title', 'Sentence'.")]
    public static object ChangeTextCase(
        [Description("Scope: 'current_view', 'all', or a view name")] string scope = "current_view",
        [Description("Capitalization style: UPPER, lower, Title, Sentence")] string capitalizationStyle = "UPPER")
    {
        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");

        IEnumerable<TextNote> textNotes;
        if (scope.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            textNotes = new FilteredElementCollector(doc).OfClass(typeof(TextNote)).Cast<TextNote>();
        }
        else if (scope.Equals("current_view", StringComparison.OrdinalIgnoreCase))
        {
            var activeView = RevitContext.ActiveView ?? throw new McpException("No active view.");
            textNotes = new FilteredElementCollector(doc, activeView.Id).OfClass(typeof(TextNote)).Cast<TextNote>();
        }
        else
        {
            var view = new FilteredElementCollector(doc).OfClass(typeof(View))
                .Cast<View>().FirstOrDefault(v => !v.IsTemplate && v.Name.Equals(scope, StringComparison.OrdinalIgnoreCase))
                ?? throw new McpException($"View '{scope}' not found.");
            textNotes = new FilteredElementCollector(doc, view.Id).OfClass(typeof(TextNote)).Cast<TextNote>();
        }

        var notes = textNotes.ToList();
        using var tx = new Transaction(doc, "Capitalize Text Notes");
        tx.Start();
        var updated = 0;
        foreach (var tn in notes)
        {
            if (string.IsNullOrEmpty(tn.Text)) continue;
            tn.Text = capitalizationStyle.ToUpperInvariant() switch
            {
                "UPPER" => tn.Text.ToUpperInvariant(),
                "LOWER" => tn.Text.ToLowerInvariant(),
                "TITLE" => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(tn.Text.ToLowerInvariant()),
                "SENTENCE" => char.ToUpperInvariant(tn.Text[0]) + tn.Text[1..].ToLowerInvariant(),
                _ => tn.Text,
            };
            updated++;
        }
        tx.Commit();
        return new { status = "Success", updatedCount = updated };
    }
}
