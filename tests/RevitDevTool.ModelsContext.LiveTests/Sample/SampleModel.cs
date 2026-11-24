using DevTools.ModelsContext.Documents;
using RevitDevTool.Core;
using RevitDevTool.Tools.ModelsContext.Documents;

namespace RevitDevTool.ModelsContext.LiveTests;

internal sealed record SampleDocument(DocumentKey Key, SourceId Source, Document Document, string Label);

internal static class SampleModel
{
    public static string HostTitle => CaseFiles.Facts.HostTitle;

    public static string ArchitecturalTitle => CaseFiles.Facts.LinkTitle;

    public static UIApplication Application => RevitContext.UiApplication;

    public static Document Host
    {
        get
        {
            if (Application.ActiveUIDocument?.Document is { } active
                && active.Title.StartsWith(HostTitle, StringComparison.OrdinalIgnoreCase))
            {
                return active;
            }

            Assert.Inconclusive($"Open '{HostTitle}.rvt' as the active Revit model.");
            return null!;
        }
    }

    public static SampleDocument Current
    {
        get
        {
            var host = Host;
            return new SampleDocument(DocumentRegistry.KeyOf(host), DocumentRegistry.SourceOf(host), host, "HVAC");
        }
    }

    public static SampleDocument Architectural => LinkTitled(ArchitecturalTitle, "Arch");

    public static SampleDocument Site => LinkTitled(CaseFiles.Facts.SiteLinkTitle, "Site");

    private static SampleDocument LinkTitled(string title, string label)
    {
        var host = Host;
        foreach (var instance in new FilteredElementCollector(host).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>())
        {
            if (instance.GetLinkDocument() is { } link
                && link.Title.StartsWith(title, StringComparison.OrdinalIgnoreCase))
            {
                return new SampleDocument(DocumentRegistry.KeyOf(host, instance), DocumentRegistry.LinkSourceOf(instance),
                    link, label);
            }
        }

        Assert.Inconclusive($"Load the '{title}' link in '{HostTitle}'.");
        return null!;
    }

    public static IReadOnlyList<SampleDocument> All
    {
        get
        {
            var host = Host;
            var documents = new List<SampleDocument> { Current };
            foreach (var instance in new FilteredElementCollector(host).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>())
            {
                if (instance.GetLinkDocument() is { } link)
                {
                    documents.Add(new SampleDocument(DocumentRegistry.KeyOf(host, instance), DocumentRegistry.LinkSourceOf(instance),
                        link, link.Title));
                }
            }

            return documents;
        }
    }

    public static IReadOnlyList<SampleDocument> Of(string docs)
    {
        return docs switch
        {
            "Host" => [Current],
            "Arch" => [Architectural],
            "Both" => [Current, Architectural],
            _ => throw new ArgumentException($"Unknown documents '{docs}'; use Host, Arch or Both.", nameof(docs))
        };
    }

    public static SourceId SourceOf(string document)
    {
        return document switch
        {
            "Host" => Current.Source,
            "Arch" => Architectural.Source,
            "Missing" => new SourceId(CaseFiles.Facts.MissingSource),
            _ => new SourceId(document)
        };
    }

    public static HashSet<SourceId> KnownSources => [.. All.Select(document => document.Source)];
}
