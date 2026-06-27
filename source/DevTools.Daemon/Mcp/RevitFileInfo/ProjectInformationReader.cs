using System.IO.Compression;
using System.Xml.Linq;

namespace DevTools.Daemon.Mcp.RevitFileInfo;

internal static class ProjectInformationReader
{
    private const string StreamName = "ProjectInformation";

    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace A = "urn:schemas-autodesk-com:partatom";

    public static ProjectInformation? Read(RevitCompoundFile file)
    {
        using var ms = file.TryReadStream(StreamName);
        if (ms is null) return null;

        XDocument doc;
        try
        {
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true);
            var xmlEntry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
            if (xmlEntry is null) return null;

            using var xmlStream = xmlEntry.Open();
            doc = XDocument.Load(xmlStream);
        }
        catch
        {
            return null;
        }

        var entry = doc.Root;
        if (entry is null) return null;

        var title = entry.Element(Atom + "title")?.Value;
        var updated = entry.Element(Atom + "updated")?.Value;

        var designFile = entry.Descendants(A + "design-file").FirstOrDefault();
        var product = designFile?.Element(A + "product")?.Value;
        var productVersion = designFile?.Element(A + "product-version")?.Value;

        var parameters = ReadIdentityParameters(entry);

        return new ProjectInformation
        {
            Title = title,
            Updated = updated,
            Product = product,
            ProductVersion = productVersion,
            Parameters = parameters,
        };
    }

    private static IReadOnlyDictionary<string, string> ReadIdentityParameters(XElement entry)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var group in entry.Descendants(A + "group"))
        {
            foreach (var el in group.Elements())
            {
                if (el.Name.Namespace == A) continue;

                var displayName = (string?)el.Attribute("displayName") ?? el.Name.LocalName;
                var value = el.Value;
                if (!string.IsNullOrEmpty(value))
                    result.TryAdd(displayName, value);
            }
        }

        return result;
    }
}

[PublicAPI]
internal sealed record ProjectInformation
{
    public string? Title { get; init; }
    public string? Updated { get; init; }
    public string? Product { get; init; }
    public string? ProductVersion { get; init; }
    public IReadOnlyDictionary<string, string> Parameters { get; init; } =
        new Dictionary<string, string>();
}
