using System.Text;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Routing.Native;

public sealed class NativeHostResourceProxy : McpServerResource
{
    private readonly IHostMcpSession session;
    private readonly string originalUri;

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
    }

    public override ResourceTemplate ProtocolResourceTemplate { get; }
    public override Resource? ProtocolResource { get; }
    public override IReadOnlyList<object> Metadata => [];
    public override bool IsMatch(string uri) => string.Equals(uri, ProtocolResourceTemplate.UriTemplate, StringComparison.Ordinal);
    public override ValueTask<ReadResourceResult> ReadAsync(RequestContext<ReadResourceRequestParams> request, CancellationToken cancellationToken = default) =>
        new(session.ReadResourceAsync(originalUri, cancellationToken));

    private static string CreateUri(int processId, string originalUri) =>
        $"devtools://host/{processId}/resource/{ToBase64Url(Encoding.UTF8.GetBytes(originalUri))}";

    private static string ToBase64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
