using System.Reflection;
using DevTools.Mcp.Catalog;
using ModelContextProtocol.Protocol;

namespace DevTools.Agents.Revit.Resources;

/// <summary>
/// Provides a Revit Python cheat sheet as an MCP resource.
/// AI clients can read this before writing Python code to reduce trial-and-error.
/// </summary>
public sealed class RevitPythonCheatsheet : IBuiltInMcpResource
{
    private static readonly Lazy<string> Content = new(LoadEmbeddedContent);

    public string UriTemplate => "revit://python-cheatsheet";

    public Resource ProtocolResource { get; } = new()
    {
        Uri = "revit://python-cheatsheet",
        Name = "Revit Python Cheatsheet",
        Description = "Revit Python.NET patterns, builtins, transactions, queries, and PEP 723 deps. Read before writing execute_python_code.",
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
            .FirstOrDefault(n => n.EndsWith("revit-python-cheatsheet.md", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            return "# Revit Python Cheatsheet\n\nEmbedded content not found.";

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
