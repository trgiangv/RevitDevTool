using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Mcp.Server.Prompts;

/// <summary>Daemon-owned fixed prompt for AutoCAD .NET command generation.</summary>
public static class AcadCodePrompt
{
    private const string Name = "acad_code";

    public static McpServerPrompt Create() =>
        McpServerPrompt.Create(Get, new McpServerPromptCreateOptions
        {
            Name = Name,
            Description = "Generate AutoCAD .NET command C# code for an automation task."
        });

    [Description("Generate AutoCAD .NET command C# code for an automation task.")]
    private static GetPromptResult Get(
        [Description("What the code should accomplish in AutoCAD")] string task,
        [Description("Operation mode: modify (transaction with commit) or readonly")] string? mode = null)
    {
        mode ??= "modify";
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
