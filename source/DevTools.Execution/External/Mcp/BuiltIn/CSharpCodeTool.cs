using System.Text.Json;
using DevTools.McpParser.Models;
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
            "Compile and execute C# code in the running host process.\n" +
            "Revit: implement IExternalCommand — set the 'message' ref parameter for output.\n" +
            "AutoCAD: use [CommandMethod].\n" +
            "Host API assemblies are auto-referenced. Use #r for extras, #r \"nuget:\" for packages.",
        InputSchema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                code = new
                {
                    type = "string",
                    description =
                        "C# code with a host command class. " +
                        "Revit: IExternalCommand (output via 'message' ref param). " +
                        "AutoCAD: [CommandMethod]. Host assemblies auto-referenced. #r for extras."
                }
            },
            required = new[] { "code" }
        }),
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
        if (!doc.RootElement.TryGetProperty("code", out var codeElement) ||
            codeElement.ValueKind != JsonValueKind.String)
        {
            return McpToolExecutionResult.Failed(
                ExecutionErrorCodes.ToolInvokeFailed, "Missing required 'code' parameter.");
        }

        var code = codeElement.GetString();
        if (string.IsNullOrWhiteSpace(code))
            return McpToolExecutionResult.Failed(
                ExecutionErrorCodes.ToolInvokeFailed, "Code parameter must not be empty.");

        var result = await executor.ExecuteAsync(code!, ct).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            var errorResult = new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = result.Error ?? "Execution failed." }]
            };
            return McpToolExecutionResult.Completed(errorResult, $"Failed '{Name}'.");
        }

        var callResult = new CallToolResult
        {
            Content = [new TextContentBlock { Text = result.Output }]
        };
        return McpToolExecutionResult.Completed(callResult, $"Completed '{Name}'.");
    }
}
