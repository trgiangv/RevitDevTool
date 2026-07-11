using System.Text.Json;
using DevTools.Mcp.BuiltIn;
using ModelContextProtocol.Protocol;

namespace DevTools.Agents.Acad.Prompts;

/// <summary>
/// MCP prompt that generates structured AutoCAD .NET command code.
/// AI clients call this prompt to get a well-formed instruction for code generation.
/// </summary>
public sealed class AcadCodePrompt : IBuiltInMcpPrompt
{
    public Prompt ProtocolPrompt { get; } = new()
    {
        Name = "acad_code",
        Description = "Generate AutoCAD .NET command C# code for an automation task.",
        Arguments =
        [
            new PromptArgument { Name = "task", Description = "What the code should accomplish in AutoCAD", Required = true },
            new PromptArgument { Name = "mode", Description = "Operation mode: modify (transaction with commit) or readonly (read-only transaction)", Required = false }
        ]
    };

    public GetPromptResult Get(IReadOnlyDictionary<string, JsonElement>? arguments)
    {
        var task = arguments?.TryGetValue("task", out var taskEl) == true ? taskEl.GetString() ?? "query entities" : "query entities";
        var mode = arguments?.TryGetValue("mode", out var modeEl) == true ? modeEl.GetString() ?? "modify" : "modify";

        var isReadonly = mode.Equals("readonly", StringComparison.OrdinalIgnoreCase);
        var instructions = isReadonly
            ? "Use OpenMode.ForRead only. Do NOT call tr.Commit() — just read data and output results."
            : "Use OpenMode.ForWrite for modifications. Call tr.Commit() to persist changes.";

        return new GetPromptResult
        {
            Description = $"Generate AutoCAD .NET command: {task}",
            Messages =
            [
                new PromptMessage
                {
                    Role = Role.User,
                    Content = new TextContentBlock
                    {
                        Text = $"Write C# code for AutoCAD's execute_csharp_code tool.\n\n" +
                               $"Task: {task}\n\n" +
                               $"Requirements:\n" +
                               $"- Use [CommandMethod(\"TOOLCMD\")] attribute on the Execute method\n" +
                               $"- Get Document, Database, Editor from Application.DocumentManager.MdiActiveDocument\n" +
                               $"- {instructions}\n" +
                               $"- Include all required usings (Autodesk.AutoCAD.DatabaseServices, Geometry, ApplicationServices, EditorInput, Runtime)\n" +
                               $"- Use ed.WriteMessage() for output\n" +
                               $"- For entities: AppendEntity + AddNewlyCreatedDBObject\n" +
                               $"- For modifications: GetObject with OpenMode.ForWrite\n\n" +
                               $"Return ONLY the complete C# code."
                    }
                }
            ]
        };
    }
}
