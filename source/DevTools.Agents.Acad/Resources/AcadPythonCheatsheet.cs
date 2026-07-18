using System.IO;
using System.Reflection;
using System.ComponentModel;
using DevTools.Mcp.BuiltIn;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Agents.Acad.Resources;

/// <summary>
/// Provides an AutoCAD Python cheat sheet as an MCP resource.
/// AI clients can read this before writing Python code to reduce trial-and-error.
/// </summary>
public sealed class AcadPythonCheatsheet : IBuiltInMcpResource
{
    private static readonly Lazy<string> Content = new(LoadEmbeddedContent);

    public McpServerResource Primitive => McpServerResource.Create(typeof(AcadPythonCheatsheet).GetMethod(nameof(ReadPythonCheatsheet))!, this);

    [McpServerResource(UriTemplate = "acad://python-cheatsheet", Name = "acad_python_cheatsheet")]
    public ReadResourceResult ReadPythonCheatsheet()
    {
        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = "acad://python-cheatsheet",
                    MimeType = "text/markdown",
                    Text = Content.Value
                }
            ]
        };
    }

    private static string LoadEmbeddedContent()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("acad-python-cheatsheet.md", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            return "# AutoCAD Python Cheatsheet\n\nEmbedded content not found.";

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
