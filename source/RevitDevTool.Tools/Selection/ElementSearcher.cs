namespace RevitDevTool.Tools.Selection;

/// <summary>
/// Batch-first element resolve. <see cref="SearchTokens"/> is the engine;
/// <see cref="TrySearch"/> is the single-token wrapper (monitor click = batch size 1).
/// </summary>
public static class ElementSearcher
{
    /// <summary>
    /// Primary entry: parse all tokens, group by target document / kind, one
    /// collector pass for IFC GUIDs, then return matches for a single Select.
    /// </summary>
    public static IReadOnlyList<SearchMatch> SearchTokens(
        Document hostDocument,
        IEnumerable<string> tokens,
        string? defaultLinkInstanceId = null)
    {
        ArgumentNullException.ThrowIfNull(hostDocument);
        ArgumentNullException.ThrowIfNull(tokens);

        List<TokenParser.ParsedToken>? parsed = null;
        foreach (var token in tokens)
        {
            if (TokenParser.TryParse(token, defaultLinkInstanceId) is not { } p)
                continue;
            parsed ??= [];
            parsed.Add(p);
        }

        return parsed is null ? [] : SearchParsed(hostDocument, parsed);
    }

    /// <summary>
    /// Single already-classified token (monitor click). Same engine, batch size 1.
    /// </summary>
    public static SearchMatch? TrySearch(
        Document document,
        TokenKind kind,
        string value,
        string? linkInstanceId)
    {
        ArgumentNullException.ThrowIfNull(document);
        return SearchParsed(document, [new TokenParser.ParsedToken(kind, value, linkInstanceId)])
            .FirstOrDefault();
    }

    private static List<SearchMatch> SearchParsed(
        Document hostDocument,
        IReadOnlyList<TokenParser.ParsedToken> parsed)
    {
        var byDoc = GroupByDocument(hostDocument, parsed);
        return byDoc.Count == 0 ? [] : BuildMatches(byDoc);
    }

    private static Dictionary<Document, DocBucket> GroupByDocument(
        Document hostDocument,
        IReadOnlyList<TokenParser.ParsedToken> parsed)
    {
        var byDoc = new Dictionary<Document, DocBucket>();
        foreach (var token in parsed)
        {
            if (!TryResolveDocument(hostDocument, token.LinkInstanceId, out var targetDoc, out var linkInstance))
                continue;

            if (!byDoc.TryGetValue(targetDoc, out var bucket))
            {
                bucket = new DocBucket(linkInstance);
                byDoc[targetDoc] = bucket;
            }

            bucket.Add(token.Kind, token.Value);
        }

        return byDoc;
    }

    private static List<SearchMatch> BuildMatches(Dictionary<Document, DocBucket> byDoc)
    {
        var matches = new List<SearchMatch>(byDoc.Count);
        foreach (var (document, bucket) in byDoc)
        {
            var ids = ResolveBucket(document, bucket);
            if (ids.Count == 0)
                continue;

            matches.Add(bucket.LinkInstance is null
                ? new SearchMatch.HostElements(ids)
                : new SearchMatch.LinkedElements(bucket.LinkInstance, ids));
        }

        return matches;
    }

    private static HashSet<ElementId> ResolveBucket(Document document, DocBucket bucket)
    {
        var ids = new HashSet<ElementId>();

        foreach (var value in bucket.ElementIds)
        {
            if (FindByElementId(document, value) is { } found)
                ids.UnionWith(found);
        }

        foreach (var value in bucket.UniqueIds)
        {
            if (FindByUniqueId(document, value) is { } found)
                ids.UnionWith(found);
        }

        if (bucket.IfcGuids.Count > 0)
        {
            var found = FindByIfcGuids(document, bucket.IfcGuids);
            ids.UnionWith(found);
        }

        return ids;
    }

    private sealed class DocBucket(RevitLinkInstance? linkInstance)
    {
        public RevitLinkInstance? LinkInstance { get; } = linkInstance;
        public HashSet<string> ElementIds { get; } = new(StringComparer.Ordinal);
        public HashSet<string> UniqueIds { get; } = new(StringComparer.Ordinal);
        public HashSet<string> IfcGuids { get; } = new(StringComparer.Ordinal);

        public void Add(TokenKind kind, string value)
        {
            switch (kind)
            {
                case TokenKind.ElementId:
                    ElementIds.Add(value);
                    break;
                case TokenKind.UniqueId:
                    UniqueIds.Add(value);
                    break;
                case TokenKind.IfcGuid:
                    IfcGuids.Add(value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }
    }

    private static bool TryResolveDocument(
        Document host,
        string? linkInstanceIdText,
        out Document document,
        out RevitLinkInstance? instance)
    {
        if (linkInstanceIdText is null)
        {
            document = host;
            instance = null;
            return true;
        }

        document = host;
        instance = null;
        if (!TokenParser.TryParseElementId(linkInstanceIdText, out var instanceId))
            return false;
        if (host.GetElement(instanceId) is not RevitLinkInstance linkInstance)
            return false;

        var linkDoc = linkInstance.GetLinkDocument();
        if (linkDoc is null)
            return false;

        document = linkDoc;
        instance = linkInstance;
        return true;
    }

    private static ICollection<ElementId>? FindByElementId(Document document, string value)
    {
        if (!TokenParser.TryParseElementId(value, out var id))
            return null;
        return document.GetElement(id) is null ? null : [id];
    }

    private static ICollection<ElementId>? FindByUniqueId(Document document, string value)
    {
        var element = document.GetElement(value);
        return element is null ? null : [element.Id];
    }

    /// <summary>
    /// One FEC: <see cref="LogicalOrFilter"/> of IFC_GUID / IFC_TYPE_GUID equals for the whole set.
    /// </summary>
    private static ICollection<ElementId> FindByIfcGuids(Document document, HashSet<string> guids)
    {
        if (guids.Count == 0)return [];

        var guidProvider = new ParameterValueProvider(new ElementId(BuiltInParameter.IFC_GUID));
        var typeGuidProvider = new ParameterValueProvider(new ElementId(BuiltInParameter.IFC_TYPE_GUID));
        var equals = new FilterStringEquals();

        var filters = new List<ElementFilter>(guids.Count * 2);

        foreach (var guid in guids)
        {
            filters.Add(new ElementParameterFilter(new FilterStringRule(guidProvider, equals, guid)));
            filters.Add(new ElementParameterFilter(new FilterStringRule(typeGuidProvider, equals, guid)));
        }

        var filter = new LogicalOrFilter(filters);

        return new FilteredElementCollector(document).WherePasses(filter).ToElementIds();
    }
}
