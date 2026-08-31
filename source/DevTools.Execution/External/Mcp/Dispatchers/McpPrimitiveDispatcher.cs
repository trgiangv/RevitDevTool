using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using DevTools.Execution.Providers.Python;
using DevTools.Mcp.Adapter.Execution;
using DevTools.Mcp.Catalog.Bridging;
using DevTools.Mcp.Core.Invocation;
using DevTools.Mcp.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using DevTools.Mcp.Core.Results;
using DevTools.Telemetry;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Python.Runtime;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.Execution.External.Mcp.Dispatchers;

/// <summary>
/// Unified dispatcher for MCP primitive invocations (tools, resources).
/// Routes to .NET assembly, Python, or built-in C# execution backends.
/// </summary>
public sealed class McpPrimitiveDispatcher(
    IServiceProvider serviceProvider,
    PythonExecutor executor,
    DotnetMethodResolver methodResolver,
    IEnumerable<IBuiltInMcpTool> builtInTools,
    IEnumerable<IBuiltInMcpResource> builtInResources,
    ITelemetry telemetry) : IMcpPrimitiveDispatcher
{
    private readonly ConcurrentDictionary<string, (MethodInfo Method, object? Target)> _cachedToolInvocations =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, McpServerResource> _cachedResources = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IBuiltInMcpTool> _builtInToolIndex = builtInTools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IBuiltInMcpResource> _builtInResourceIndex = builtInResources.ToDictionary(r => r.UriTemplate, StringComparer.OrdinalIgnoreCase);

    #region Tools

    public async Task<McpResult<McpInvocationResponse>> DispatchToolAsync(
        McpRegisteredTool tool,
        CallToolRequestParams request,
        IHostContextExecutor hostContext,
        CancellationToken ct = default)
    {
        telemetry.RecordMcpInvocation(tool.Binding.SourceKind.ToString());

        try
        {
            var normalizedPayload = PythonInvocationPayload.ToJson(request);

            return tool.Binding.SourceKind switch
            {
                ExecutionMode.Dotnet => await hostContext
                    .ExecuteAsync(() => InvokeDotnetTool(tool, request, ct), ct)
                    .ConfigureAwait(false),
                ExecutionMode.Python => await hostContext
                    .ExecuteAsync(() => InvokePythonTool(tool, normalizedPayload), ct)
                    .ConfigureAwait(false),
                ExecutionMode.CSharp => await InvokeCSharpToolAsync(tool.Descriptor.Name, request, ct)
                    .ConfigureAwait(false),
                _ => McpResult<McpInvocationResponse>.Failure(new McpError(McpErrorCode.ExecutionFailed,
                    $"Unknown or unsupported MCP tool execution: '{tool.Binding.SourceKind}'.", []))
            };
        }
        catch (InputRequiredException ex)
        {
            return McpResult<McpInvocationResponse>.Success(ToolsetMrtrBridge.ToInputRequiredResponse(ex));
        }
        catch (Exception ex) when (ToolsetMrtrBridge.IsForeignInputRequired(ex))
        {
            return McpResult<McpInvocationResponse>.Success(
                ToolsetMrtrBridge.ToInputRequiredResponse(ToolsetMrtrBridge.ToHostException(ex)));
        }
        catch (Exception ex)
        {
            if (TelemetryReporting.ShouldReportCriticalException(ex))
            {
                telemetry.RecordCriticalException(
                    ex,
                    TelemetryKeys.Feature.Mcp,
                    new Dictionary<string, string> { [TelemetryKeys.Tag.Provider] = tool.Binding.SourceKind.ToString() });
            }

            return McpResult<McpInvocationResponse>.Failure(new McpError(McpErrorCode.ExecutionFailed, ex.Message, []));
        }
    }

    private McpResult<McpInvocationResponse> InvokeDotnetTool(
        McpRegisteredTool tool,
        CallToolRequestParams request,
        CancellationToken ct)
    {
        var sdkRequest = SdkInvocationRequest.ToToolContext(tool.Descriptor.Name, request);
        var invocation = GetOrCreateToolInvocation(tool);
        if (invocation is null)
            return McpResult<McpInvocationResponse>.Failure(new McpError(McpErrorCode.ExecutionFailed,
                $"No .NET tool method mapped for '{tool.Descriptor.Name}'.", []));

        try
        {
            var raw = ToolsetInvoker.InvokeRaw(
                invocation.Value.Method,
                invocation.Value.Target,
                sdkRequest,
                serviceProvider,
                ct);
            var response = ToolsetResultSerializer.ToInvocationResponse(raw, tool.Descriptor.OutputSchema);
            return McpResult<McpInvocationResponse>.Success(response);
        }
        catch (InputRequiredException ex)
        {
            return McpResult<McpInvocationResponse>.Success(ToolsetMrtrBridge.ToInputRequiredResponse(ex));
        }
        catch (Exception ex) when (ToolsetMrtrBridge.IsForeignInputRequired(ex))
        {
            return McpResult<McpInvocationResponse>.Success(
                ToolsetMrtrBridge.ToInputRequiredResponse(ToolsetMrtrBridge.ToHostException(ex)));
        }
    }

    private (MethodInfo Method, object? Target)? GetOrCreateToolInvocation(McpRegisteredTool tool)
    {
        if (_cachedToolInvocations.TryGetValue(tool.Id, out var cached))
            return cached;

        var method = methodResolver.ResolveTool(tool);
        if (method is null)
            return null;

        var target = method.IsStatic
            ? null
            : ActivatorUtilities.CreateInstance(serviceProvider, method.DeclaringType!);
        var entry = (method, target);
        _cachedToolInvocations.TryAdd(tool.Id, entry);
        return entry;
    }

    private McpResult<McpInvocationResponse> InvokePythonTool(McpRegisteredTool tool, string normalizedPayload)
    {
        var binding = tool.Binding;
        if (string.IsNullOrWhiteSpace(binding.SourcePath) || !File.Exists(binding.SourcePath))
            throw new InvalidOperationException($"Python MCP source file was not found: {binding.SourcePath}.");

        var rootFolder = Path.GetDirectoryName(binding.SourcePath) ?? string.Empty;
        var resultJson = executor.Execute(
            binding.SourcePath, rootFolder,
            scope =>
            {
                scope.Set(PythonInstances.ToolName, new PyString(tool.Descriptor.Name));
                scope.Set(PythonInstances.PayloadJson, new PyString(normalizedPayload));
                scope.Exec(PythonEmbedded.ToolInvokeScript);
                return scope.Get(PythonInstances.ResultJson).As<string>();
            });
        var callResult = PythonResultParser.ParseCallToolResult(resultJson);
        return McpResult<McpInvocationResponse>.Success(
            ToolsetResultSerializer.ToInvocationResponse(callResult, tool.Descriptor.OutputSchema));
    }

    private async Task<McpResult<McpInvocationResponse>> InvokeCSharpToolAsync(
        string toolName,
        CallToolRequestParams request,
        CancellationToken ct)
    {
        if (!_builtInToolIndex.TryGetValue(toolName, out var tool))
            return McpResult<McpInvocationResponse>.Failure(new McpError(
                McpErrorCode.ExecutionFailed, $"No C# tool registered for '{toolName}'.", []));

        var sdkRequest = SdkInvocationRequest.ToToolContext(toolName, request);
        var result = await tool.ServerTool.InvokeAsync(sdkRequest, ct).ConfigureAwait(false);
        return McpResult<McpInvocationResponse>.Success(ToolsetResultSerializer.ToInvocationResponse(result, null));
    }

    #endregion

    #region Resources

    public ReadResourceResult ReadResource(
        McpRegisteredResource resource,
        string uri,
        CancellationToken ct = default)
    {
        return resource.Binding.SourceKind switch
        {
            ExecutionMode.Dotnet => InvokeDotnetResource(resource, uri, ct),
            ExecutionMode.Python => InvokePythonResource(resource, uri),
            ExecutionMode.CSharp => InvokeCSharpResource(resource, uri),
            _ => throw new InvalidOperationException($"Unsupported resource execution source '{resource.Binding.SourceKind}'.")
        };
    }

    private ReadResourceResult InvokeCSharpResource(McpRegisteredResource resource, string uri)
    {
        var resourceUri = resource.Descriptor?.Uri ?? resource.TemplateDescriptor?.UriTemplate ?? string.Empty;
        if (!_builtInResourceIndex.TryGetValue(resourceUri, out var builtIn))
            throw new InvalidOperationException($"No built-in resource registered for '{resourceUri}'.");
        return builtIn.Read(uri);
    }

    private ReadResourceResult InvokeDotnetResource(
        McpRegisteredResource resource,
        string uri,
        CancellationToken ct)
    {
        var request = SdkInvocationRequest.ToResourceContext(uri);
        var serverResource = DotnetMcpServerFactory.GetOrCreate(
            _cachedResources, resource.Id, resource, methodResolver.ResolveResource, serviceProvider,
            (method, target) => McpServerResource.Create(
                method,
                target,
                McpCatalogCreateOptions.ForResource(resource, serviceProvider)));

        if (serverResource is null)
            throw new InvalidOperationException(
                $"No .NET resource method mapped for '{resource.DisplayName}'.");

        var name = resource.DisplayName;
        return DotnetMcpServerFactory.GetCompletedResult(serverResource.ReadAsync(request, ct), "resource", name);
    }

    private ReadResourceResult InvokePythonResource(McpRegisteredResource resource, string uri)
    {
        var sourcePath = resource.Binding.SourcePath;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new InvalidOperationException($"Python MCP source file was not found: {sourcePath}.");

        var rootFolder = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var resultJson = executor.Execute(
            sourcePath, rootFolder,
            scope =>
            {
                var name = resource.DisplayName;
                scope.Set(PythonInstances.Operation, new PyString(PythonInstances.OperationResource));
                scope.Set(PythonInstances.ResourceName, new PyString(name));
                scope.Set(PythonInstances.ResourceUri, new PyString(uri));
                scope.Exec(PythonEmbedded.ToolInvokeScript);
                return scope.Get(PythonInstances.ResultJson).As<string>();
            });
        return PythonResultParser.ParseReadResourceResult(resultJson);
    }

    #endregion

    public void ClearCaches()
    {
        _cachedToolInvocations.Clear();
        _cachedResources.Clear();
    }
}
