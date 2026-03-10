using System.Reflection;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using RevitDevTool.Contracts;
using RevitDevTool.Mcp.Interfaces;
namespace RevitDevTool.Mcp.Dotnet;

public sealed class McpDotnetToolInvoker(
    DotnetToolMethodResolver methodResolver,
    IServiceProvider serviceProvider) : McpToolInvokerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public override bool CanHandle(ExecutionMode executionMode)
    {
        return executionMode == ExecutionMode.Assembly;
    }

    protected override Task<McpToolExecutionResult> ExecuteCoreAsync(
        McpToolDefinition definition,
        string normalizedPayload,
        IProgress<McpProgressUpdate>? progress,
        CancellationToken cancellationToken)
    {
        var startedAtUtc = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        progress?.Report(new McpProgressUpdate
        {
            Stage = "Binding",
            Message = $"Binding .NET MCP tool '{definition.Name}'..."
        });

        var method = methodResolver.Resolve(definition);
        if (method is null)
        {
            return Task.FromResult(McpToolExecutionResult.Failed(
                "tool.not_implemented",
                $"No .NET tool method mapped for '{definition.Name}'.",
                metadata: BuildMetadata(definition, startedAtUtc, stopwatch.ElapsedMilliseconds)));
        }

        try
        {
            using var doc = JsonDocument.Parse(normalizedPayload);
            var args = BindArguments(method, doc.RootElement);
            var target = method.IsStatic
                ? null
                : ActivatorUtilities.CreateInstance(serviceProvider, method.DeclaringType!);

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new McpProgressUpdate
            {
                Stage = "Executing",
                Message = $"Executing .NET MCP tool '{definition.Name}'..."
            });

            var result = method.Invoke(target, args);
            var unwrappedResult = UnwrapMethodResult(result);
            var payload = unwrappedResult is null ? "{}" : JsonSerializer.Serialize(unwrappedResult, JsonOptions);
            var resultKind = DetermineResultKind(unwrappedResult);
            stopwatch.Stop();

            return Task.FromResult(McpToolExecutionResult.Succeeded(
                payload,
                $"Completed '{definition.Name}'.",
                resultKind,
                BuildMetadata(definition, startedAtUtc, stopwatch.ElapsedMilliseconds),
                progressUpdates:
                [
                    new McpProgressUpdate
                    {
                        Stage = "Completed",
                        Message = $"Completed .NET MCP tool '{definition.Name}'.",
                    }
                ]));
        }
        catch (InvalidOperationException ex)
        {
            stopwatch.Stop();
            return Task.FromResult(McpToolExecutionResult.Failed(
                "tool.invalid_arguments",
                ex.Message,
                ex.StackTrace,
                metadata: BuildMetadata(definition, startedAtUtc, stopwatch.ElapsedMilliseconds)));
        }
        catch (JsonException ex)
        {
            stopwatch.Stop();
            return Task.FromResult(McpToolExecutionResult.Failed(
                "tool.invalid_payload",
                ex.Message,
                ex.StackTrace,
                metadata: BuildMetadata(definition, startedAtUtc, stopwatch.ElapsedMilliseconds)));
        }
        catch (TargetInvocationException ex)
        {
            stopwatch.Stop();
            var message = ex.InnerException?.Message ?? ex.Message;
            var details = ex.InnerException?.StackTrace ?? ex.StackTrace;
            return Task.FromResult(McpToolExecutionResult.Failed(
                "tool.invoke_failed",
                message,
                details,
                metadata: BuildMetadata(definition, startedAtUtc, stopwatch.ElapsedMilliseconds)));
        }
    }

    private static object? UnwrapMethodResult(object? value)
    {
        if (value is not Task task)
            return value;

        task.GetAwaiter().GetResult();
        var taskType = task.GetType();
        return !taskType.IsGenericType
            ? new { ok = true }
            : taskType.GetProperty("Result")?.GetValue(task);
    }

    private static string DetermineResultKind(object? value)
    {
        if (value is null)
            return McpResultKinds.Empty;

        return value is string ? McpResultKinds.Text : McpResultKinds.Json;
    }

    private static McpToolExecutionMetadata BuildMetadata(McpToolDefinition definition, DateTime startedAtUtc, long durationMs)
    {
        return new McpToolExecutionMetadata
        {
            ToolId = definition.ToolId,
            ToolName = definition.Name,
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = startedAtUtc.AddMilliseconds(durationMs),
            DurationMs = durationMs
        };
    }

    private static object?[] BindArguments(MethodInfo method, JsonElement root)
    {
        var parameters = method.GetParameters();
        var args = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            if (TryGetPropertyCaseInsensitive(root, parameter.Name ?? string.Empty, out var value))
            {
                args[i] = JsonSerializer.Deserialize(value.GetRawText(), parameter.ParameterType, JsonOptions);
                continue;
            }

            if (parameter.HasDefaultValue)
            {
                args[i] = parameter.DefaultValue;
                continue;
            }

            var isNullable = !parameter.ParameterType.IsValueType ||
                             Nullable.GetUnderlyingType(parameter.ParameterType) is not null;
            if (isNullable)
            {
                args[i] = null;
                continue;
            }

            throw new InvalidOperationException(
                $"Missing required parameter '{parameter.Name}' for tool '{method.Name}'.");
        }

        return args;
    }

    private static bool TryGetPropertyCaseInsensitive(
        JsonElement root, string propertyName, out JsonElement value)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
