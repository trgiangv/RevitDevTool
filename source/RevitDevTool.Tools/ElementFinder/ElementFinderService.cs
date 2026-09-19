using RevitDevTool.Tools.Selection;

namespace RevitDevTool.Tools.ElementFinder;

/// <summary>
/// Multiline token parse + clipboard format for <see cref="ElementFinderViewModel"/>.
/// Resolve stays in <see cref="Selection.ElementSearcher"/>.
/// </summary>
public static class ElementFinderService
{
    public static List<string> ParseTokens(string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return [];

        // Revit "IDs of Selection" default is comma-separated (often ", ").
        // Also accept newlines / tabs / semicolons / spaces. Do not split on '@'
        // so Linkify <c>instanceId@inner</c> stays one token.
        return searchText!.Split(
                ['\r', '\n', '\t', ';', ',', ' '],
                StringSplitOptions.RemoveEmptyEntries)
            .Select(static t => t.Trim())
            .Where(static t => t.Length > 0)
            .ToList();
    }

    /// <summary>
    /// Formats current selection tokens. Linked elements use <c>instanceId@value</c>.
    /// </summary>
    public static string FormatSelected(UIDocument uiDocument, TokenKind kind)
    {
        ArgumentNullException.ThrowIfNull(uiDocument);

        var lines = new List<string>();
        foreach (var (element, linkInstance) in EnumerateSelectedElements(uiDocument))
        {
            var value = kind switch
            {
                TokenKind.ElementId => TokenParser.FormatElementId(element.Id),
                TokenKind.UniqueId => element.UniqueId,
                TokenKind.IfcGuid => TryGetIfcGuid(element),
                _ => null
            };

            if (string.IsNullOrWhiteSpace(value))
                continue;

            lines.Add(linkInstance is null
                ? value!
                : $"{TokenParser.FormatElementId(linkInstance.Id)}@{value}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static IEnumerable<(Element Element, RevitLinkInstance? LinkInstance)> EnumerateSelectedElements(
        UIDocument uiDocument)
    {
        var hostDoc = uiDocument.Document;

#if REVIT2023_OR_GREATER
        var references = uiDocument.Selection.GetReferences();
        if (references.Count > 0)
        {
            foreach (var reference in references)
            {
                if (reference.LinkedElementId != ElementId.InvalidElementId)
                {
                    if (hostDoc.GetElement(reference.ElementId) is not RevitLinkInstance instance)
                        continue;
                    var linkDoc = instance.GetLinkDocument();
                    var linked = linkDoc?.GetElement(reference.LinkedElementId);
                    if (linked is not null)
                        yield return (linked, instance);
                    continue;
                }

                if (reference.ElementId == ElementId.InvalidElementId)
                    continue;

                var hostElement = hostDoc.GetElement(reference.ElementId);
                if (hostElement is not null)
                    yield return (hostElement, null);
            }

            yield break;
        }
#endif

        foreach (var id in uiDocument.Selection.GetElementIds())
        {
            var element = hostDoc.GetElement(id);
            if (element is not null)
                yield return (element, null);
        }
    }

    private static string? TryGetIfcGuid(Element element)
    {
        var guid = element.get_Parameter(BuiltInParameter.IFC_GUID)?.AsString();
        if (!string.IsNullOrWhiteSpace(guid))
            return guid;

        guid = element.get_Parameter(BuiltInParameter.IFC_TYPE_GUID)?.AsString();
        return string.IsNullOrWhiteSpace(guid) ? null : guid;
    }
}
