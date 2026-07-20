using System.Text;
using System.Text.RegularExpressions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Routing.Native;

public sealed class NativeHostResourceProxy : McpServerResource
{
    private static readonly Regex UriVariableRegex = new(@"\{[^}]+\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IHostMcpSession session;
    private readonly string originalUri;
    private readonly Regex? nativeUriParser;

    public NativeHostResourceProxy(IHostMcpSession session, Resource? resource, ResourceTemplate? template)
    {
        this.session = session;
        originalUri = resource?.Uri ?? template?.UriTemplate ?? throw new ArgumentException("A resource or resource template is required.");
        var nativeUri = CreateUri(session.Instance.ProcessId, originalUri);
        var name = resource?.Name ?? template!.Name;
        var description = resource?.Description ?? template?.Description;
        var mimeType = resource?.MimeType ?? template?.MimeType;
        ProtocolResourceTemplate = new ResourceTemplate
        {
            Name = name,
            Description = description,
            MimeType = mimeType,
            UriTemplate = nativeUri,
            Meta = NativeHostMetadata.Create(session.Instance, originalUri)
        };
        ProtocolResource = resource is null ? null : new Resource
        {
            Name = name,
            Description = description,
            MimeType = mimeType,
            Uri = nativeUri,
            Meta = NativeHostMetadata.Create(session.Instance, originalUri)
        };
        nativeUriParser = IsTemplate ? CreateUriTemplateRegex(nativeUri) : null;
    }

    public override ResourceTemplate ProtocolResourceTemplate { get; }
    public override Resource? ProtocolResource { get; }
    public override IReadOnlyList<object> Metadata => [];
    public override bool IsMatch(string uri) => nativeUriParser?.IsMatch(uri)
        ?? string.Equals(uri, ProtocolResourceTemplate.UriTemplate, StringComparison.Ordinal);

    public override ValueTask<ReadResourceResult> ReadAsync(RequestContext<ReadResourceRequestParams> request, CancellationToken cancellationToken = default) =>
        new(session.ReadResourceAsync(TranslateToOriginalUri(request.Params.Uri), cancellationToken));

    private bool IsTemplate => UriVariableRegex.IsMatch(originalUri);

    private string TranslateToOriginalUri(string nativeUri)
    {
        if (nativeUriParser is null)
            return originalUri;

        var match = nativeUriParser.Match(nativeUri);
        if (!match.Success)
            throw new ArgumentException($"URI '{nativeUri}' does not match the resource template.", nameof(nativeUri));

        var literalParts = UriVariableRegex.Split(originalUri);
        var original = new StringBuilder(literalParts[0]);
        for (var index = 1; index < literalParts.Length; index++)
        {
            original.Append(match.Groups[index].Value);
            original.Append(literalParts[index]);
        }

        return original.ToString();
    }

    private static string CreateUri(int processId, string originalUri)
    {
        var prefix = $"devtools://host/{processId}/resource/";
        if (!UriVariableRegex.IsMatch(originalUri))
            return prefix + ToBase64Url(Encoding.UTF8.GetBytes(originalUri));

        var literalParts = UriVariableRegex.Split(originalUri);
        var variables = UriVariableRegex.Matches(originalUri);
        var uri = new StringBuilder(prefix).Append(ToBase64Url(Encoding.UTF8.GetBytes(literalParts[0])));
        for (var index = 0; index < variables.Count; index++)
        {
            uri.Append(variables[index].Value);
            uri.Append(ToBase64Url(Encoding.UTF8.GetBytes(literalParts[index + 1])));
        }

        return uri.ToString();
    }

    private static Regex CreateUriTemplateRegex(string uriTemplate)
    {
        var literalParts = UriVariableRegex.Split(uriTemplate);
        var pattern = new StringBuilder("^");
        for (var index = 0; index < literalParts.Length; index++)
        {
            pattern.Append(Regex.Escape(literalParts[index]));
            if (index < literalParts.Length - 1)
                pattern.Append("([^/?#]*)");
        }

        return new Regex(pattern.Append('$').ToString(), RegexOptions.CultureInvariant);
    }

    private static string ToBase64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
