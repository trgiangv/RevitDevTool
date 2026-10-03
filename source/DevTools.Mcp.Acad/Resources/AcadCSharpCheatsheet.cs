using System.IO;
using System.Reflection;
using DevTools.Mcp;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Acad.Resources;

/// <summary>
/// Provides an AutoCAD C# cheat sheet as an MCP resource.
/// AI clients can read this before writing C# code to reduce trial-and-error.
/// </summary>
public sealed class AcadCSharpCheatsheet : IBuiltInMcpResource
{
    private static readonly Lazy<string> Content = new(LoadEmbeddedContent);

    public string UriTemplate => McpSpecKeys.Resource.AcadCSharpCheatsheet;

    public Resource ProtocolResource { get; } = new()
    {
        Uri = McpSpecKeys.Resource.AcadCSharpCheatsheet,
        Name = "AutoCAD C# Cheatsheet",
        Description = $"Common AutoCAD C# API patterns, transaction usage, entity creation, layer operations, and selection. Read before writing {McpSpecKeys.Tool.ExecuteCSharp}.",
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
            .FirstOrDefault(n => n.EndsWith("acad-csharp-cheatsheet.md", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            return "# AutoCAD API Cheat Sheet\n\nEmbedded content not found.";

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
