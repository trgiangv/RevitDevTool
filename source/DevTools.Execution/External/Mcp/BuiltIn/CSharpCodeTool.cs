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
            "Compile and execute C# code in the running host process.\n" +
            "Revit: implement IExternalCommand. Output via 'message' ref param.\n" +
            "AutoCAD: use [CommandMethod].\n" +
            "Host API assemblies auto-referenced. Use #r for extras, #r \"nuget:\" for packages.\n\n" +
            "Example (Revit):\n" +
            "```\n" +
            "using System.Linq;\n" +
            "using Autodesk.Revit.DB;\n" +
            "using Autodesk.Revit.UI;\n" +
            "using Autodesk.Revit.Attributes;\n\n" +
            "[Transaction(TransactionMode.Manual)]\n" +
            "public class Command : IExternalCommand\n" +
            "{\n" +
            "    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)\n" +
            "    {\n" +
            "        var doc = commandData.Application.ActiveUIDocument.Document;\n" +
            "        // your code here\n" +
            "        message = \"result output\";\n" +
            "        return Result.Succeeded;\n" +
            "    }\n" +
            "}\n" +
            "```\n\n" +
            "Error types: compilation errors (fix code and retry), runtime exceptions (check logic), " +
            "or transaction rollback (unresolvable Revit constraint violation).\n" +
            "For read-only queries, use [Transaction(TransactionMode.ReadOnly)].",
        InputSchema = McpSchemaBuilder.Object(
        [
            McpSchemaBuilder.String(
                IpcPropertyNames.Code,
                "C# code implementing IExternalCommand (Revit) or [CommandMethod] (AutoCAD). " +
                "Must include using directives, Transaction attribute, and class definition. " +
                "Set 'message' ref param for output.")
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
