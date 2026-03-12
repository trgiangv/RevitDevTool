using System.Text.RegularExpressions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace RevitDevTool.Server;

public static class RevitResourceAdapter
{
    public static McpServerResource ToMcpServerResource(
        Resource? resource,
        ResourceTemplate? resourceTemplate,
        string resourceId,
        RevitBridgeClient bridgeClient)
    {
        return new BridgedMcpServerResource(resource, resourceTemplate, resourceId, bridgeClient);
    }
}

internal sealed class BridgedMcpServerResource : McpServerResource
{
    private readonly string _resourceId;
    private readonly RevitBridgeClient _bridgeClient;
    private readonly Regex? _uriParser;

    public BridgedMcpServerResource(
        Resource? resource,
        ResourceTemplate? resourceTemplate,
        string resourceId,
        RevitBridgeClient bridgeClient)
    {
        _resourceId = resourceId;
        _bridgeClient = bridgeClient;

        if (resourceTemplate is not null)
        {
            ProtocolResourceTemplate = resourceTemplate;
            ProtocolResource = resourceTemplate.AsResource();
        }
        else if (resource is not null)
        {
            ProtocolResource = resource;
            ProtocolResourceTemplate = new ResourceTemplate
            {
                Name = resource.Name,
                UriTemplate = resource.Uri,
                Description = resource.Description,
                MimeType = resource.MimeType,
            };
        }
        else
        {
            throw new ArgumentException("Either resource or resourceTemplate must be non-null.");
        }

        _uriParser = ProtocolResourceTemplate.UriTemplate.Contains('{')
            ? CreateUriTemplateRegex(ProtocolResourceTemplate.UriTemplate)
            : null;
    }

    private static Regex CreateUriTemplateRegex(string uriTemplate)
    {
        var literalParts = Regex.Split(uriTemplate, @"\{[^}]+\}");
        var patternBuilder = new System.Text.StringBuilder("^");
        for (var i = 0; i < literalParts.Length; i++)
        {
            patternBuilder.Append(Regex.Escape(literalParts[i]));
            if (i < literalParts.Length - 1)
                patternBuilder.Append("([^/?#]*)");
        }
        patternBuilder.Append('$');
        return new Regex(patternBuilder.ToString(), RegexOptions.IgnoreCase);
    }

    public override bool IsMatch(string uri)
    {
        if (_uriParser is null)
            return string.Equals(uri, ProtocolResourceTemplate.UriTemplate, StringComparison.OrdinalIgnoreCase);
        return _uriParser.IsMatch(uri);
    }

    public override ResourceTemplate ProtocolResourceTemplate { get; }

    public override Resource? ProtocolResource { get; }

    public override IReadOnlyList<object> Metadata => [];

    public override ValueTask<ReadResourceResult> ReadAsync(
        RequestContext<ReadResourceRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var name = ProtocolResource?.Name ?? ProtocolResourceTemplate.Name;
        return new ValueTask<ReadResourceResult>(_bridgeClient.ReadResourceAsync(
            _resourceId,
            name,
            request.Params?.Uri ?? ProtocolResourceTemplate.UriTemplate,
            cancellationToken));
    }
}
