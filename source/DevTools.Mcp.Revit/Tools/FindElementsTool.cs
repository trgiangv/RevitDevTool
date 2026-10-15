using System.ComponentModel;
using DevTools.Execution.Abstractions;
using DevTools.Mcp.Core.Protocol;
using DevTools.Mcp.Core.Utils;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RevitDevTool.Core;
using RevitDevTool.Tools.ElementFinder;
using RevitDevTool.Tools.Selection;

namespace DevTools.Mcp.Revit.Tools;

/// <summary>
/// Resolves Element Finder tokens through <see cref="ElementSearcher.SearchTokens"/>
/// and returns one hit row per match for the next <c>code_mode</c> call.
/// </summary>
public sealed class FindElementsTool : IBuiltInMcpTool
{
    private readonly IHostContextExecutor _hostContext;

    public FindElementsTool(IHostContextExecutor hostContext)
    {
        _hostContext = hostContext;
        ServerTool = McpServerTool.Create(
            FindAsync,
            new McpServerToolCreateOptions
            {
                Name = McpSpecKeys.Tool.FindElements,
                Title = "Find Elements",
                Description =
                    """
                    Element Finder tokens in both directions. query accepts them, and each hit returns them.

                    Query
                    - One token or many.
                    - Separators: newline, comma, semicolon, tab, space.
                    - Do not split on '@'.

                    Tokens
                    - elementId: positive decimal. Example: 445566
                    - uniqueId: 45-char hex, 8-4-4-4-12-8.
                    - ifcGuid: 22 chars, starts with 0-3, contains a digit or '_' or '$'.
                    - Host: the value alone.
                    - Link: {linkInstanceId}@{value}. Example: 8821@445566
                    - linkInstanceId is the host ElementId of a RevitLinkInstance. Exactly one '@'.

                    Scope
                    - A bare token searches the host, or the linkInstanceId argument when that is set.
                    - A scoped token searches that link.

                    Result
                    - hits: input, kind, elementId, uniqueId, ifcGuid, name, category.
                    - elementId, uniqueId, and ifcGuid are tokens. Any of them can be passed back as query.
                    - elementId null means that input missed. One IFC token can return several hits.
                    - links appears only on a miss: linkInstanceId, title, loaded.
                    - Retry a bare miss as {linkInstanceId}@{input}.

                    Revit API
                    - Split one '@'. Left is the link instance. Right is the GetElement argument.
                    """,
                ReadOnly = true,
                Destructive = false,
                OpenWorld = false
            });
    }

    public string Name => McpSpecKeys.Tool.FindElements;
    public McpServerTool ServerTool { get; }

    [Description("Resolve Element Finder tokens. Keep {linkInstanceId}@{inner} as one token.")]
    private async Task<CallToolResult> FindAsync(
        [Description(
            "One token or many. Separators: newline, comma, semicolon, tab, space. Do not split on '@'. " +
            "Each token is an ElementId, UniqueId, IFC GUID, or {linkInstanceId}@{inner}.")]
        string query,
        [Description("Host ElementId of a RevitLinkInstance. Applies only to bare tokens.")]
        string? linkInstanceId = null,
        CancellationToken cancellationToken = default)
    {
        return await _hostContext.ExecuteAsync(
            () => Find(query, linkInstanceId), cancellationToken).ConfigureAwait(false);
    }

    private static CallToolResult Find(string? query, string? linkInstanceId)
    {
        var host = RevitContext.ActiveUiDocument?.Document;
        if (host is null)
            return ToolHelpers.ErrorResult("No active document.");

        var inputs = ElementFinderService.ParseTokens(query);
        if (inputs.Count == 0)
            return ToolHelpers.ErrorResult("query is empty. Pass ElementId, UniqueId, IFC GUID, or {linkInstanceId}@{inner}.");

        var scope = string.IsNullOrWhiteSpace(linkInstanceId) ? null : linkInstanceId!.Trim();
        var links = ListLinks(host);
        if (scope is not null && links.All(link => link.LinkInstanceId != scope))
            return UnknownLink(scope, links);

        var hits = Search(host, inputs, scope);
        return hits.Any(hit => hit.ElementId is null)
            ? ToolHelpers.Result(new { hits, links })
            : ToolHelpers.Result(new { hits });
    }

    private static CallToolResult UnknownLink(string scope, LinkInfo[] links) =>
        ToolHelpers.ErrorResult(ToolHelpers.Serialize(new
        {
            error = "Unknown linkInstanceId. Prefix tokens as {linkInstanceId}@{inner}, or pick an id from links.",
            linkInstanceId = scope,
            links
        }));

    private static LinkInfo[] ListLinks(Document host) =>
        new FilteredElementCollector(host)
            .OfClass(typeof(RevitLinkInstance))
            .Cast<RevitLinkInstance>()
            .Select(link => new LinkInfo(
                TokenParser.FormatElementId(link.Id),
                link.GetLinkDocument()?.Title ?? link.Name,
                link.GetLinkDocument() is not null))
            .ToArray();

    private static MatchInfo[] Search(Document host, IReadOnlyList<string> inputs, string? scope)
    {
        var located = Locate(host, ElementSearcher.SearchTokens(host, inputs, scope));
        return inputs.SelectMany(input => Rows(input, TokenParser.TryParse(input, scope), located)).ToArray();
    }

    private static IEnumerable<MatchInfo> Rows(
        string input,
        TokenParser.ParsedToken? token,
        List<(string? LinkId, Element Element)> located)
    {
        if (token is not { } known)
        {
            yield return MatchInfo.Miss(input);
            yield break;
        }

        var matched = false;
        foreach (var (linkId, element) in located)
        {
            if (!string.Equals(linkId, known.LinkInstanceId, StringComparison.Ordinal) || !Matches(element, known.Kind, known.Value))
                continue;

            matched = true;
            yield return MatchInfo.Found(input, known.Kind.ToString(), linkId, element);
        }

        if (!matched)
            yield return MatchInfo.Miss(input, known.Kind.ToString());
    }

    private static List<(string? LinkId, Element Element)> Locate(Document host, IReadOnlyList<SearchMatch> matches)
    {
        var located = new List<(string? LinkId, Element Element)>();
        foreach (var match in matches)
        {
            switch (match)
            {
                case SearchMatch.HostElements hostMatch:
                    Take(located, host, null, hostMatch.Ids);
                    break;
                case SearchMatch.LinkedElements linked:
                    Take(located, linked.Instance.GetLinkDocument(), TokenParser.FormatElementId(linked.Instance.Id), linked.Ids);
                    break;
            }
        }

        return located;
    }

    private static void Take(
        List<(string? LinkId, Element Element)> located,
        Document? document,
        string? linkId,
        ICollection<ElementId> ids)
    {
        if (document is null)
            return;

        foreach (var id in ids)
        {
            if (document.GetElement(id) is { } element)
                located.Add((linkId, element));
        }
    }

    private static bool Matches(Element element, TokenKind kind, string value)
    {
        return kind switch
        {
            TokenKind.ElementId => TokenParser.FormatElementId(element.Id) == value,
            TokenKind.UniqueId => string.Equals(element.UniqueId, value, StringComparison.OrdinalIgnoreCase),
            TokenKind.IfcGuid => string.Equals(ReadIfcGuid(element), value, StringComparison.Ordinal)
                                  || string.Equals(ParameterText(element, BuiltInParameter.IFC_TYPE_GUID), value, StringComparison.Ordinal),
            _ => false
        };
    }

    private static string? ReadIfcGuid(Element element) =>
        ParameterText(element, BuiltInParameter.IFC_GUID)
        ?? ParameterText(element, BuiltInParameter.IFC_TYPE_GUID);

    private static string? ParameterText(Element element, BuiltInParameter parameter)
    {
        var text = element.get_Parameter(parameter)?.AsString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    [UsedImplicitly]
    private sealed record LinkInfo(string LinkInstanceId, string Title, bool Loaded);

    [UsedImplicitly]
    private sealed record MatchInfo(
        string Input,
        string? Kind,
        string? ElementId,
        string? UniqueId,
        string? IfcGuid,
        string? Name,
        string? Category)
    {
        public static MatchInfo Miss(string input, string? kind = null) => new(input, kind, null, null, null, null, null);

        public static MatchInfo Found(string input, string kind, string? scope, Element element)
        {
            return new MatchInfo(
                input,
                kind,
                Token(TokenParser.FormatElementId(element.Id)),
                Token(element.UniqueId),
                Token(ReadIfcGuid(element)),
                element.Name,
                element.Category?.Name);
            string? Token(string? value) => value is null ? null : scope is null ? value : $"{scope}@{value}";
        }
    }
}
