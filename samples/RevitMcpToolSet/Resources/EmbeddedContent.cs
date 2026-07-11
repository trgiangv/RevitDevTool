using System.Reflection;

namespace RevitMcpToolSet.Resources;

internal static class EmbeddedContent
{
    private static readonly Assembly Assembly = typeof(EmbeddedContent).Assembly;

    public static string Capabilities => Load("capabilities.md");
    public static string PatternsQuery => Load("patterns-query.md");
    public static string PatternsMep => Load("patterns-mep.md");
    public static string PatternsDocumentation => Load("patterns-documentation.md");
    public static string PatternsExport => Load("patterns-export.md");
    public static string Errors => Load("errors.md");
    public static string Units => Load("units.md");

    private static string Load(string fileName)
    {
        var resourceName = $"RevitMcpToolSet.Resources.Content.{fileName}";
        using var stream = Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
