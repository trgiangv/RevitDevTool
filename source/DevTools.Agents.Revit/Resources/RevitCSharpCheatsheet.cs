using System.Reflection;
using System.ComponentModel;
using DevTools.Mcp.BuiltIn;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Agents.Revit.Resources;

/// <summary>
/// Provides a Revit C# cheat sheet as an MCP resource.
/// AI clients can read this before writing C# code to reduce trial-and-error.
/// </summary>
public sealed class RevitCSharpCheatsheet : IBuiltInMcpResource
{
    private static readonly Lazy<string> Content = new(LoadEmbeddedContent);

    public McpServerResource Primitive => McpServerResource.Create(typeof(RevitCSharpCheatsheet).GetMethod(nameof(ReadCSharpCheatsheet))!, this);

    [McpServerResource(UriTemplate = "revit://csharp-cheatsheet", Name = "revit_csharp_cheatsheet")]
    public ReadResourceResult ReadCSharpCheatsheet()
    {
        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = "revit://csharp-cheatsheet",
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
