using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using DevTools.Execution.External.Mcp.Execution;
using DevTools.Execution.Providers.Python;
using DevTools.McpParser.Models;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Python.Runtime;
namespace DevTools.Execution.External.Mcp.Dispatchers;

/// <summary>
/// Dispatches MCP prompt calls asynchronously via the dotnet backend,
/// or synchronously under the GIL for the Python backend.
/// </summary>
public sealed class PromptExecutionDispatcher(
    IServiceProvider serviceProvider, PythonExecutor executor, DotnetMethodResolver methodResolver) : ICacheable
{
    private static readonly JsonSerializerOptions JsonOptions = McpJsonUtilities.DefaultOptions;
    private readonly ConcurrentDictionary<string, McpServerPrompt> _cachedPrompts = new(StringComparer.OrdinalIgnoreCase);

    public GetPromptResult GetPrompt(
        McpRegisteredPrompt prompt,
        IReadOnlyDictionary<string, JsonElement>? arguments,
        CancellationToken ct = default)
    {
        // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
        return prompt.Binding.SourceKind switch
        {
            ExecutionMode.Assembly => InvokeDotnetPrompt(prompt, arguments, ct),
            ExecutionMode.Python => InvokePythonPrompt(prompt, arguments),
            _ => throw new InvalidOperationException($"Unsupported prompt execution source '{prompt.Binding.SourceKind}'.")
        };
    }

    public void ClearCache() => _cachedPrompts.Clear();

    private GetPromptResult InvokeDotnetPrompt(
        McpRegisteredPrompt prompt,
        IReadOnlyDictionary<string, JsonElement>? arguments,
        CancellationToken ct)
    {
        var serverPrompt = GetOrCreateServerPrompt(prompt);
        if (serverPrompt is null)
        {
            throw new InvalidOperationException(
                $"No .NET prompt method mapped for '{prompt.ProtocolPrompt.Name}'.");
        }

        var requestParams = new GetPromptRequestParams
        {
            Name = prompt.ProtocolPrompt.Name,
            Arguments = arguments?.ToDictionary(kv => kv.Key, kv => kv.Value)
        };

        var requestContext = RequestContextFactory.Create(
            requestParams,
            RequestMethods.PromptsGet);

        return DotnetMcpServerFactory.GetCompletedResult(
            serverPrompt.GetAsync(requestContext, ct),
            "prompt",
            prompt.ProtocolPrompt.Name);
    }

    private McpServerPrompt? GetOrCreateServerPrompt(McpRegisteredPrompt prompt)
    {
        return DotnetMcpServerFactory.GetOrCreate(
            _cachedPrompts,
            prompt.Id,
            prompt,
            methodResolver.ResolvePrompt,
            serviceProvider,
            (method, target) => McpServerPrompt.Create(method, target));
    }

    private GetPromptResult InvokePythonPrompt(
        McpRegisteredPrompt prompt,
        IReadOnlyDictionary<string, JsonElement>? arguments)
    {
        var sourcePath = prompt.Binding.SourcePath;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new InvalidOperationException($"Python MCP source file was not found: {sourcePath}.");

        var rootFolder = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var resultJson = executor.Execute(
            sourcePath,
            rootFolder,
            scope =>
            {
                scope.Set(PythonInstances.Operation, new PyString(PythonInstances.OperationPrompt));
                scope.Set(PythonInstances.PromptName, new PyString(prompt.ProtocolPrompt.Name));
                scope.Set(PythonInstances.ArgumentsJson, new PyString(SerializeJson(arguments ?? new Dictionary<string, JsonElement>())));
                scope.Exec(PythonEmbedded.ToolInvokeScript);
                return scope.Get(PythonInstances.ResultJson).As<string>();
            });

        return JsonSerializer.Deserialize<GetPromptResult>(resultJson, JsonOptions)
               ?? new GetPromptResult { Description = prompt.ProtocolPrompt.Description };
    }

    private static string SerializeJson<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
}
