using System.Text.Json;
using DevTools.Execution.Providers.Python;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Execution.External.Mcp.Registry;

public sealed class PythonMcpServerResource(McpRegisteredResource registration, PythonExecutor executor) : McpServerResource
{
    public override Resource? ProtocolResource => registration.ProtocolResource;
    public override ResourceTemplate ProtocolResourceTemplate => registration.ProtocolTemplate ?? new ResourceTemplate
    {
        UriTemplate = registration.ProtocolResource?.Uri ?? throw new InvalidOperationException("Python MCP resource has no URI."),
        Name = registration.ProtocolResource?.Name ?? string.Empty,
        Description = registration.ProtocolResource?.Description
    };
    public override IReadOnlyList<object> Metadata => [];
    public override bool IsMatch(string uri) => UriMatches(ProtocolResourceTemplate.UriTemplate, uri);

    public override ValueTask<ReadResourceResult> ReadAsync(RequestContext<ReadResourceRequestParams> request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resultJson = PythonMcpInvoker.InvokeResource(executor, registration, request.Params.Uri);
        var result = JsonSerializer.Deserialize<ReadResourceResult>(resultJson, McpJsonUtilities.DefaultOptions)
            ?? throw new JsonException("Python MCP resource returned an empty result.");
        return new ValueTask<ReadResourceResult>(result);
    }

    private static bool UriMatches(string template, string uri)
    {
        var pattern = "^" + System.Text.RegularExpressions.Regex.Replace(
            System.Text.RegularExpressions.Regex.Escape(template), "\\\\\\{[^}]+\\\\\\}", "[^/]+") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(uri, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }
}

internal sealed class PythonMcpServerPrimitiveAdapter(PythonExecutor executor) : IMcpServerPrimitiveAdapter
{
    public ExecutionMode SourceKind => ExecutionMode.Python;
    public McpServerTool? CreateTool(McpRegisteredTool registration) => new PythonMcpServerTool(registration, executor);
    public McpServerPrompt? CreatePrompt(McpRegisteredPrompt registration) => new PythonMcpServerPrompt(registration, executor);
    public McpServerResource? CreateResource(McpRegisteredResource registration) => new PythonMcpServerResource(registration, executor);
}
