using System.IO;
using System.Text.Json;
using DevTools.Execution.Providers.Python;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Python.Runtime;

namespace DevTools.Execution.External.Mcp.Registry;

public sealed class PythonMcpServerTool(McpRegisteredTool registration, PythonExecutor executor) : McpServerTool
{
    public override Tool ProtocolTool => registration.ProtocolTool;
    public override IReadOnlyList<object> Metadata => [];

    public override ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyDictionary<string, JsonElement> arguments = request.Params.Arguments is { } values
            ? values.ToDictionary(pair => pair.Key, pair => pair.Value)
            : new Dictionary<string, JsonElement>();
        var resultJson = PythonMcpInvoker.InvokeTool(executor, registration, arguments);
        var result = JsonSerializer.Deserialize<CallToolResult>(resultJson, McpJsonUtilities.DefaultOptions)
            ?? throw new JsonException("Python MCP tool returned an empty result.");
        return new ValueTask<CallToolResult>(result);
    }
}

internal static class PythonMcpInvoker
{
    public static string InvokeTool(PythonExecutor executor, McpRegisteredTool registration, IReadOnlyDictionary<string, JsonElement> arguments) =>
        Invoke(executor, registration.Binding.SourcePath, scope =>
        {
            scope.Set(PythonInstances.ToolName, new PyString(registration.ProtocolTool.Name));
            scope.Set(PythonInstances.PayloadJson, new PyString(JsonSerializer.Serialize(arguments, McpJsonUtilities.DefaultOptions)));
        });

    public static string InvokePrompt(PythonExecutor executor, McpRegisteredPrompt registration, IReadOnlyDictionary<string, JsonElement> arguments) =>
        Invoke(executor, registration.Binding.SourcePath, scope =>
        {
            scope.Set(PythonInstances.Operation, new PyString(PythonInstances.OperationPrompt));
            scope.Set(PythonInstances.PromptName, new PyString(registration.ProtocolPrompt.Name));
            scope.Set(PythonInstances.ArgumentsJson, new PyString(JsonSerializer.Serialize(arguments, McpJsonUtilities.DefaultOptions)));
        });

    public static string InvokeResource(PythonExecutor executor, McpRegisteredResource registration, string uri) =>
        Invoke(executor, registration.Binding.SourcePath, scope =>
        {
            scope.Set(PythonInstances.Operation, new PyString(PythonInstances.OperationResource));
            scope.Set(PythonInstances.ResourceName, new PyString(registration.ProtocolResource?.Name ?? registration.ProtocolTemplate?.Name ?? string.Empty));
            scope.Set(PythonInstances.ResourceUri, new PyString(uri));
        });

    private static string Invoke(PythonExecutor executor, string? sourcePath, Action<PyModule> configure)
    {
        var path = sourcePath ?? throw new InvalidOperationException("Python MCP source file was not found.");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new InvalidOperationException($"Python MCP source file was not found: {sourcePath}.");

        return executor.Execute(path, Path.GetDirectoryName(path) ?? string.Empty, scope =>
        {
            configure(scope);
            scope.Exec(PythonEmbedded.ToolInvokeScript);
            return scope.Get(PythonInstances.ResultJson).As<string>();
        });
    }
}
