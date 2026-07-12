using System.IO;
using System.Reflection;
using DevTools.Mcp.BuiltIn;
using ModelContextProtocol.Protocol;

namespace DevTools.Agents.Acad.Resources;

/// <summary>
/// Provides an AutoCAD Python cheat sheet as an MCP resource.
/// AI clients can read this before writing Python code to reduce trial-and-error.
/// </summary>
public sealed class AcadPythonCheatsheet : IBuiltInMcpResource
{
    private static readonly Lazy<string> Content = new(LoadEmbeddedContent);

    public string UriTemplate => "acad://python-cheatsheet";

    public Resource ProtocolResource { get; } = new()
    {
        Uri = "acad://python-cheatsheet",
        Name = "AutoCAD Python Cheatsheet",
        Description = "AutoCAD Python.NET patterns, builtins, transactions, and PEP 723 deps. Read before writing execute_python_code.",
        MimeType = "text/markdown"
    };

    public ReadResourceResult Read(string uri)
    {
        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = uri,
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
