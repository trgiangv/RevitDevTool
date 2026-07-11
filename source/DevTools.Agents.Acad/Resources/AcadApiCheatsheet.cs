using System.IO;
using System.Reflection;
using DevTools.Mcp.BuiltIn;
using ModelContextProtocol.Protocol;

namespace DevTools.Agents.Acad.Resources;

/// <summary>
/// Provides an AutoCAD API cheat sheet as an MCP resource.
/// AI clients can read this before writing C# code to reduce trial-and-error.
/// </summary>
public sealed class AcadApiCheatsheet : IBuiltInMcpResource
{
    private static readonly Lazy<string> Content = new(LoadEmbeddedContent);

    public string UriTemplate => "acad://api-cheatsheet";

    public Resource ProtocolResource { get; } = new()
    {
        Uri = "acad://api-cheatsheet",
        Name = "AutoCAD API Cheat Sheet",
        Description = "Common AutoCAD .NET API patterns, transaction usage, entity creation, layer operations, and selection. Read before writing execute_csharp_code.",
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
            .FirstOrDefault(n => n.EndsWith("acad-api-cheatsheet.md", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            return "# AutoCAD API Cheat Sheet\n\nEmbedded content not found.";

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
