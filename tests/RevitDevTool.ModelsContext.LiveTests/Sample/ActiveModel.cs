using DevTools.ModelsContext.Model;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext;

namespace RevitDevTool.ModelsContext.LiveTests;

internal static class ActiveModel
{
    public static Document Host
    {
        get
        {
            if (SampleModel.Application.ActiveUIDocument?.Document is { IsFamilyDocument: false } active)
            {
                return active;
            }

            Assert.Inconclusive("Open a project as the active Revit model.");
            return null!;
        }
    }

    public static IReadOnlyList<SampleDocument> Documents
    {
        get
        {
            var host = Host;
            var documents = new List<SampleDocument>
            {
                new(DocumentRegistry.KeyOf(host), DocumentRegistry.SourceOf(host), host, host.Title)
            };
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

    public static IReadOnlyList<SampleDocument> Sources => [.. Documents.GroupBy(document => document.Source).Select(group => group.First())];

    public static HashSet<SourceId> Known => [.. Documents.Select(document => document.Source)];

    public static string CategoryName(Document document, long id)
    {
        try
        {
            return Category.GetCategory(document, id.ToElementId())?.Name ?? id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
