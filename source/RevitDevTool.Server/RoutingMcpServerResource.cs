using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RevitDevTool.Contracts;

namespace RevitDevTool.Server;

public sealed partial class RoutingMcpServerResource : McpServerResource
{
    private readonly InstanceManager _instanceManager;
    private readonly Regex? _uriParser;

    public RoutingMcpServerResource(InstanceManager instanceManager, Resource? resource, ResourceTemplate? template)
    {
        _instanceManager = instanceManager;

        if (template is not null)
        {
            ProtocolResourceTemplate = template;
            ProtocolResource = template.AsResource();
        }
        else if (resource is not null)
        {
            ProtocolResource = resource;
            ProtocolResourceTemplate = new ResourceTemplate
            {
                Name = resource.Name,
                Title = resource.Title,
                Description = resource.Description,
                UriTemplate = resource.Uri,
                MimeType = resource.MimeType,
                Annotations = resource.Annotations,
                Meta = resource.Meta,
                Icons = resource.Icons,
            };
        }
        else
        {
            throw new ArgumentException("Either resource or template must be provided.");
        }

        _uriParser = ProtocolResourceTemplate.UriTemplate.Contains('{', StringComparison.Ordinal)
            ? CreateUriTemplateRegex(ProtocolResourceTemplate.UriTemplate)
            : null;
    }

    public override ResourceTemplate ProtocolResourceTemplate { get; }
    public override Resource? ProtocolResource { get; }
    public override IReadOnlyList<object> Metadata => [];

    public override bool IsMatch(string uri)
    {
        if (_uriParser is null)
            return string.Equals(uri, ProtocolResourceTemplate.UriTemplate, StringComparison.OrdinalIgnoreCase);

        return _uriParser.IsMatch(uri);
    }

    public override async ValueTask<ReadResourceResult> ReadAsync(
        RequestContext<ReadResourceRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var client = _instanceManager.GetDefault()
                     ?? throw new InvalidOperationException("Multiple Revit instances. Specify revitInstanceId.");

        var targetUri = request.Params?.Uri ?? ProtocolResourceTemplate.UriTemplate;
        var callParams = JsonSerializer.SerializeToElement(new { uri = targetUri });

        var response = await client.RequestAsync(BridgeMethods.ResourcesRead, callParams, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsError)
            throw new InvalidOperationException(response.ErrorMessage ?? "Resource read failed.");

        return response.Result is { } result
            ? JsonSerializer.Deserialize<ReadResourceResult>(result.GetRawText()) ?? throw new InvalidOperationException("Empty resource result.")
            : throw new InvalidOperationException("No result returned.");
    }

    private static Regex CreateUriTemplateRegex(string uriTemplate)
    {
        var literalParts = UriRegex().Split(uriTemplate);
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

    [GeneratedRegex(@"\{[^}]+\}")]
    private static partial Regex UriRegex();
}
