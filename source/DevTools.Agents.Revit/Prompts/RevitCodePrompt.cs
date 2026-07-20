using System.ComponentModel;
using DevTools.Mcp.BuiltIn;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Agents.Revit.Prompts;

/// <summary>
/// MCP prompt that generates structured IExternalCommand C# code requests for Revit.
/// AI clients call this prompt to get a well-formed instruction for code generation.
/// </summary>
public sealed class RevitCodePrompt : IBuiltInMcpPrompt
{
    public McpServerPrompt Primitive => McpServerPrompt.Create(typeof(RevitCodePrompt).GetMethod(nameof(GetRevitCode))!, this);

    [McpServerPrompt(Name = "revit_code")]
    [Description("Generate IExternalCommand C# code for a Revit automation task.")]
    public GetPromptResult GetRevitCode(
        [Description("What the code should accomplish in Revit.")] string task,
        [Description("Transaction mode: manual or readonly.")] string mode = "manual")
    {
        task = string.IsNullOrWhiteSpace(task) ? "query elements" : task;

        var isReadonly = mode.Equals("readonly", StringComparison.OrdinalIgnoreCase);
        var transactionAttr = isReadonly ? "TransactionMode.ReadOnly" : "TransactionMode.Manual";
        var instructions = isReadonly
            ? "Do NOT create a Transaction. Query elements and set the message output."
            : "Wrap all modifications in a Transaction (Start/Commit). Set message with results.";

        return new GetPromptResult
        {
            Description = $"Generate Revit IExternalCommand: {task}",
            Messages =
            [
                new PromptMessage
                {
                    Role = Role.User,
                    Content = new TextContentBlock
                    {
                        Text = $"Write C# code for Revit's execute_csharp_code tool.\n\n" +
                               $"Task: {task}\n\n" +
                               $"Requirements:\n" +
                               $"- Implement IExternalCommand with [Transaction({transactionAttr})]\n" +
                               $"- {instructions}\n" +
                               $"- Include all required usings (System.Linq, Autodesk.Revit.DB, Autodesk.Revit.UI, Autodesk.Revit.Attributes)\n" +
                               $"- Set 'message' ref param with structured result output\n" +
                               $"- Return Result.Succeeded on success\n" +
                               $"- Units are feet internally — convert with UnitUtils if needed\n" +
                               $"- Use FilteredElementCollector for element queries\n" +
                               $"- Use ElementId.Value (not IntegerValue) for Revit 2024+\n\n" +
                               $"Return ONLY the complete C# code."
                    }
                }
            ]
        };
    }
}
