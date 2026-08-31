using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Server.Prompts;

/// <summary>Daemon-owned fixed prompt for Revit IExternalCommand generation.</summary>
public static class RevitCodePrompt
{
    private const string Name = "revit_code";

    public static McpServerPrompt Create() =>
        McpServerPrompt.Create(Get, new McpServerPromptCreateOptions
        {
            Name = Name,
            Description = "Generate IExternalCommand C# code for a Revit automation task."
        });

    [Description("Generate IExternalCommand C# code for a Revit automation task.")]
    private static GetPromptResult Get(
        [Description("What the code should accomplish in Revit")] string task,
        [Description("Transaction mode: manual (modifications) or readonly (queries only)")] string? mode = null)
    {
        mode ??= "manual";
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
