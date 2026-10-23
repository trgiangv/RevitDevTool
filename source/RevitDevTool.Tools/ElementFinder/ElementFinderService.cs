using RevitDevTool.Tools.Selection;

namespace RevitDevTool.Tools.ElementFinder;

/// <summary>
/// Multiline token parse + clipboard format for <see cref="ElementFinderViewModel"/>.
/// Resolve stays in <see cref="RevitDevTool.Tools.Selection.ElementSearcher"/>.
/// </summary>
public static class ElementFinderService
{
    public static List<string> ParseTokens(string? searchText)
    {
        if (searchText is null || string.IsNullOrWhiteSpace(searchText))
            return [];

        // Revit "IDs of Selection" default is comma-separated (often ", ").
        // Also accept newlines / tabs / semicolons / spaces. Do not split on '@'
        // so Linkify <c>instanceId@inner</c> stays one token.
        return searchText
            .Split(['\r', '\n', '\t', ';', ',', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(static token => token.Trim())
            .Where(static token => token.Length > 0)
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

            if (value is null || string.IsNullOrWhiteSpace(value))
                continue;

            lines.Add(linkInstance is null ? value : $"{TokenParser.FormatElementId(linkInstance.Id)}@{value}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static IEnumerable<(Element Element, RevitLinkInstance? LinkInstance)> EnumerateSelectedElements(UIDocument uiDocument)
    {
        var hostDoc = uiDocument.Document;

#if REVIT2023_OR_GREATER
        var references = uiDocument.Selection.GetReferences();
        if (references.Count > 0)
            return EnumerateReferences(hostDoc, references);
#endif

        return EnumerateElementIds(hostDoc, uiDocument.Selection.GetElementIds());
    }

#if REVIT2023_OR_GREATER
    private static IEnumerable<(Element Element, RevitLinkInstance? LinkInstance)> EnumerateReferences(Document hostDoc, IEnumerable<Reference> references)
    {
        foreach (var reference in references)
        {
            if (reference.LinkedElementId == ElementId.InvalidElementId)
            {
                var hostElement = hostDoc.GetElement(reference.ElementId);
                if (hostElement is not null)
                    yield return (hostElement, null);
                continue;
            }

            if (hostDoc.GetElement(reference.ElementId) is not RevitLinkInstance instance)
                continue;

            var linkedElement = instance.GetLinkDocument()?.GetElement(reference.LinkedElementId);
            if (linkedElement is not null)
                yield return (linkedElement, instance);
        }
    }
#endif

    private static IEnumerable<(Element Element, RevitLinkInstance? LinkInstance)> EnumerateElementIds(Document hostDoc, IEnumerable<ElementId> elementIds)
    {
        foreach (var id in elementIds)
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
