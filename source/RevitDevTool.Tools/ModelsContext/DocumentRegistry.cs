using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Model;
using Nice3point.Revit.Extensions;
using System.Collections.Concurrent;

namespace RevitDevTool.Tools.ModelsContext;

internal sealed record ResolvedDocument(Document Model, RevitLinkInstance? Link, Document Target);

internal static class DocumentRegistry
{
    private const string ModelPrefix = "model:";
    private const string LinkPrefix = "link:";
    private const string UnsavedPrefix = "unsaved:";

    private static readonly ConcurrentDictionary<DocumentKey, string> Labels = new();

    public static string ModelIdOf(Document model)
    {
        return string.IsNullOrEmpty(model.PathName) ? UnsavedPrefix + model.Title : model.PathName;
    }

    public static DocumentKey KeyOf(Document model)
    {
        return DocumentKey.Current(ModelIdOf(model));
    }

    public static DocumentKey KeyOf(Document model, RevitLinkInstance instance)
    {
        return DocumentKey.Link(ModelIdOf(model), instance.Id.ToLong());
    }

    public static SourceId SourceOf(Document model)
    {
        return new SourceId(ModelPrefix + ModelIdOf(model));
    }

    public static SourceId LinkSourceOf(RevitLinkInstance instance)
    {
        var linkDocument = instance.GetLinkDocument();
        if (linkDocument is not null && !string.IsNullOrEmpty(linkDocument.PathName))
        {
            return LinkSource(linkDocument.PathName);
        }

        return LinkSource(LinkPathOf(instance) ?? instance.Name);
    }

    public static SourceId SourceOf(ResolvedDocument resolved)
    {
        return resolved.Link is { } link ? LinkSourceOf(link) : SourceOf(resolved.Model);
    }

    public static string LabelOf(DocumentKey key)
    {
        return Labels.TryGetValue(key, out var label) ? label : key.ToString();
    }

    public static string LinkNameOf(RevitLinkInstance instance)
    {
        return instance.Document.GetElement(instance.GetTypeId())?.Name ?? instance.Name;
    }

    public static Document? FindModel(UIApplication application, string modelId)
    {
        foreach (Document document in application.Application.Documents)
        {
            if (!document.IsLinked && string.Equals(ModelIdOf(document), modelId, StringComparison.Ordinal))
            {
                return document;
            }
        }

        return null;
    }

    public static ResolvedDocument? Resolve(UIApplication application, DocumentKey key)
    {
        if (FindModel(application, key.ModelId) is not { } model)
        {
            return null;
        }

        if (key.IsCurrentModel)
        {
            return Remember(key, new ResolvedDocument(model, null, model));
        }

        if (model.GetElement(key.LinkInstanceId.ToElementId()) is not RevitLinkInstance instance
            || instance.GetLinkDocument() is not { } linkDocument)
        {
            return null;
        }

        return Remember(key, new ResolvedDocument(model, instance, linkDocument));
    }

    private static ResolvedDocument Remember(DocumentKey key, ResolvedDocument resolved)
    {
        if (!Labels.ContainsKey(key))
        {
            Labels[key] = SourceOf(resolved).Label;
        }

        return resolved;
    }

    public static ResolvedDocument Require(UIApplication application, DocumentKey key)
    {
        return Resolve(application, key) ?? throw new InvalidOperationException(RevitErrors.NotOpen);
    }

    public static Document? ResolveSource(UIApplication application, SourceId source)
    {
        if (source.Value.StartsWith(ModelPrefix, StringComparison.Ordinal))
        {
            return FindModel(application, source.Value.Substring(ModelPrefix.Length));
        }

        foreach (Document document in application.Application.Documents)
        {
            if (document.IsLinked && !string.IsNullOrEmpty(document.PathName) && LinkSource(document.PathName) == source)
            {
                return document;
            }
        }

        return null;
    }

    public static Document RequireSource(UIApplication application, SourceId source)
    {
        return ResolveSource(application, source) ?? throw new InvalidOperationException(RevitErrors.NotOpen);
    }

    public static ResolvedDocument? ActiveModel(UIApplication application)
    {
        return application.ActiveUIDocument?.Document is { } model ? new ResolvedDocument(model, null, model) : null;
    }

    public static IEnumerable<IGrouping<DocumentKey, ElementRef>> GroupByDocument(
        Document model,
        IEnumerable<ElementRef> elements)
    {
        var modelId = ModelIdOf(model);
        return elements.Where(element => element.Document.ModelId == modelId).GroupBy(element => element.Document);
    }

    public static bool IsActive(UIApplication application, DocumentKey key)
    {
        return application.ActiveUIDocument?.Document is { } model
               && string.Equals(ModelIdOf(model), key.ModelId, StringComparison.Ordinal);
    }

    private static SourceId LinkSource(string path)
    {
        return new SourceId(LinkPrefix + path.ToLowerInvariant());
    }

    private static string? LinkPathOf(RevitLinkInstance instance)
    {
        try
        {
            var reference = ExternalFileUtils.GetExternalFileReference(instance.Document, instance.GetTypeId());
            return reference is null
                ? null
                : ModelPathUtils.ConvertModelPathToUserVisiblePath(reference.GetAbsolutePath());
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Link path", instance.Id);
            return null;
        }
    }
}
