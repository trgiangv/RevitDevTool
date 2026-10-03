using System.Text.RegularExpressions;
using DevTools.Execution.Abstractions;
using DevTools.Mcp.Core.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Hosting;

/// <summary>
/// SDK resource placed directly in <see cref="McpServerResourceCollection"/>.
/// URI matching is <see cref="IsMatch"/>; the catalog store does not keep a second matcher.
/// </summary>
public sealed class SdkCollectionResource : McpServerResource
{
    private static readonly Regex UriVariableRegex = new(@"\{[^}]+\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IHostContextExecutor _hostContext;
    private readonly Func<string, CancellationToken, Task<ReadResourceResult>> _read;
    private readonly Regex? _uriParser;

    private SdkCollectionResource(
        RegisteredResource registered,
        IHostContextExecutor hostContext,
        Func<string, CancellationToken, Task<ReadResourceResult>> read)
    {
        _hostContext = hostContext;
        _read = read;

        if (registered.TemplateDescriptor is { } template)
        {
            ProtocolResourceTemplate = template;
            ProtocolResource = template.AsResource();
        }
        else if (registered.Descriptor is { } resource)
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
            throw new ArgumentException("Resource registration must include a resource or template.");
        }

        _uriParser = ProtocolResourceTemplate.UriTemplate.Contains('{', StringComparison.Ordinal)
            ? CreateUriTemplateRegex(ProtocolResourceTemplate.UriTemplate)
            : null;
    }

    public override ResourceTemplate ProtocolResourceTemplate { get; }
    public override Resource? ProtocolResource { get; }
    public override IReadOnlyList<object> Metadata => [];

    public static McpServerResource Create(
        RegisteredResource registered,
        IHostContextExecutor hostContext,
        Func<string, CancellationToken, Task<ReadResourceResult>> read) =>
        new SdkCollectionResource(registered, hostContext, read);

    public override bool IsMatch(string uri) =>
        _uriParser?.IsMatch(uri) ?? string.Equals(uri, ProtocolResourceTemplate.UriTemplate, StringComparison.OrdinalIgnoreCase);

    public override async ValueTask<ReadResourceResult> ReadAsync(
        RequestContext<ReadResourceRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var uri = request.Params?.Uri ?? string.Empty;
        ExecutionGuardContext.Mode = ExecutionGuardMode.Suppress;
        return await _hostContext
            .ExecuteAsync(
                () =>
                {
                    var pending = _read(uri, cancellationToken);
                    if (!pending.IsCompleted)
                    {
                        throw new NotSupportedException(
                            $"MCP resource '{uri}' returned an incomplete task. The host dispatcher runs synchronous delegates only.");
                    }

                    return pending.GetAwaiter().GetResult();
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static Regex CreateUriTemplateRegex(string uriTemplate)
    {
        var literalParts = UriVariableRegex.Split(uriTemplate);
        var patternBuilder = new System.Text.StringBuilder("^");
        for (var i = 0; i < literalParts.Length; i++)
        {
            patternBuilder.Append(Regex.Escape(literalParts[i]));
            if (i < literalParts.Length - 1)
                patternBuilder.Append("([^/?#]*)");
        }

        patternBuilder.Append('$');
        return new Regex(patternBuilder.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
