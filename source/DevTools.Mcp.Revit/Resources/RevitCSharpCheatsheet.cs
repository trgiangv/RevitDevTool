using System.Reflection;
using DevTools.Mcp;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Revit.Resources;

/// <summary>
/// Provides a Revit C# cheat sheet as an MCP resource.
/// AI clients can read this before writing C# code to reduce trial-and-error.
/// </summary>
public sealed class RevitCSharpCheatsheet : IBuiltInMcpResource
{
    private static readonly Lazy<string> Content = new(LoadEmbeddedContent);

    public string UriTemplate => McpSpecKeys.Resource.RevitCSharpCheatsheet;

    public Resource ProtocolResource { get; } = new()
    {
        Uri = McpSpecKeys.Resource.RevitCSharpCheatsheet,
        Name = "Revit C# Cheatsheet",
        Description = $"Optional Revit C# API samples: queries, creation, units, and dialogs. {McpSpecKeys.Tool.ExecuteCSharp} already includes the required entry pattern.",
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
            .FirstOrDefault(n => n.EndsWith("revit-csharp-cheatsheet.md", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            return "# Revit API Cheat Sheet\n\nEmbedded content not found.";

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
