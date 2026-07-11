using System.Text.Json;
using DevTools.Execution.Abstractions;
using DevTools.Mcp.Schema;
using ModelContextProtocol.Protocol;

namespace DevTools.Execution.External.Mcp.BuiltIn;

/// <summary>Compiles and executes C# code in the host process.</summary>
public sealed class CSharpCodeTool(CSharpCodeExecutor executor) : IBuiltInMcpTool
{
    public string Name => "execute_csharp_code";

    public Tool ProtocolTool { get; } = new()
    {
        Name = "execute_csharp_code",
        Description =
            "Compile and execute C# code in the running host process. " +
            "Host API assemblies auto-referenced. Use #r for extras, #r \"nuget:\" for packages.\n" +
            "IMPORTANT: Read the host's API cheat sheet resource (revit://api-cheatsheet or acad://api-cheatsheet) for required code patterns.\n" +
            "Error responses: [COMPILATION ERROR] fix code, [RUNTIME ERROR] check logic, [ROLLBACK] constraint violation.",
        InputSchema = McpSchemaBuilder.Object(
        [
            McpSchemaBuilder.String(
                IpcPropertyNames.Code,
                "Complete C# source. Revit: implement IExternalCommand, set 'message' ref param. " +
                "AutoCAD: use [CommandMethod]. Include all usings and attributes.")
        ],
        required: [IpcPropertyNames.Code]),
        Annotations = new ToolAnnotations
        {
            Title = "Execute C# Code",
            DestructiveHint = true,
            OpenWorldHint = true
        }
    };

    public async Task<McpToolExecutionResult> ExecuteAsync(
        string payloadJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(payloadJson);
        if (!doc.RootElement.TryGetProperty(IpcPropertyNames.Code, out var codeElement) ||
            codeElement.ValueKind != JsonValueKind.String)
        {
            return McpToolExecutionResult.Failed(
                McpExecutionErrorCodes.ToolInvokeFailed, "Missing required 'code' parameter.");
        }

        var code = codeElement.GetString();
        if (string.IsNullOrWhiteSpace(code))
            return McpToolExecutionResult.Failed(
                McpExecutionErrorCodes.ToolInvokeFailed, "Code parameter must not be empty.");

        var result = await executor.ExecuteAsync(code!, ct).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            var errorText = FormatError(result);
            var errorResult = new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = errorText }]
            };
            return McpToolExecutionResult.Completed(errorResult, $"Failed '{Name}'.");
        }

        var output = result.Output;
        var rollback = ExecutionGuardContext.RollbackSummary;
        if (!string.IsNullOrEmpty(rollback))
            output = $"{output}\n\n⚠️ {rollback}";

        var callResult = new CallToolResult
        {
            Content = [new TextContentBlock { Text = output }]
        };
        return McpToolExecutionResult.Completed(callResult, $"Completed '{Name}'.");
    }

    private static string FormatError(CodeExecutionResult result)
    {
        var error = result.Error ?? "Execution failed.";

        if (error.Contains(": error CS"))
            return $"[COMPILATION ERROR] Fix the code and retry.\n{error}";

        if (error.Contains("rolled back", StringComparison.OrdinalIgnoreCase))
            return $"[ROLLBACK] Transaction failed due to unresolvable Revit constraint.\n{error}";

        return $"[RUNTIME ERROR] {error}";
    }
}
