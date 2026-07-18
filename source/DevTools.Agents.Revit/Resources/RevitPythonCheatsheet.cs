using System.Reflection;
using System.ComponentModel;
using DevTools.Mcp.BuiltIn;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Agents.Revit.Resources;

/// <summary>
/// Provides a Revit Python cheat sheet as an MCP resource.
/// AI clients can read this before writing Python code to reduce trial-and-error.
/// </summary>
public sealed class RevitPythonCheatsheet : IBuiltInMcpResource
{
    private static readonly Lazy<string> Content = new(LoadEmbeddedContent);

    public McpServerResource Primitive => McpServerResource.Create(typeof(RevitPythonCheatsheet).GetMethod(nameof(ReadPythonCheatsheet))!, this);

    [McpServerResource(UriTemplate = "revit://python-cheatsheet", Name = "revit_python_cheatsheet")]
    public ReadResourceResult ReadPythonCheatsheet()
    {
        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = "revit://python-cheatsheet",
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
            .FirstOrDefault(n => n.EndsWith("revit-python-cheatsheet.md", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            return "# Revit Python Cheatsheet\n\nEmbedded content not found.";

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
