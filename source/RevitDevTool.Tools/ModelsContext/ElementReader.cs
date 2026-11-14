using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Model;
using Nice3point.Revit.Extensions;
using System.Diagnostics;
using DocumentVersion = DevTools.ModelsContext.Model.DocumentVersion;
using ElementRecord = DevTools.ModelsContext.Model.ElementRecord;

namespace RevitDevTool.Tools.ModelsContext;

internal sealed class ElementReader(Document document, HashSet<long> knownTypes)
{
    public const int SliceSize = 5000;
    public static readonly TimeSpan SliceTime = TimeSpan.FromMilliseconds(30);

    private const long NoTypeBase = -1_000_000_000_000L;
    private const string NoType = "No type";

    private readonly HashSet<long> _modelCategories = [.. ModelCategoryIds(document).Select(id => id.ToLong())];

    public List<TypeRecord> Types { get; } = [];

    public List<ElementRecord> Elements { get; } = [];

    public static List<ElementId> ModelCategoryIds(Document document)
    {
        var ids = new List<ElementId>();
        foreach (Category category in document.Settings.Categories)
        {
            if (category.CategoryType == CategoryType.Model && category.BuiltInCategory != BuiltInCategory.OST_RvtLinks)
            {
                ids.Add(category.Id);
            }
        }

        return ids;
    }

    public static ElementFilter ModelFilter(
        Document document,
        ICollection<ElementId>? categories = null,
        IReadOnlyList<ElementFilter>? options = null)
    {
        var filters = new List<ElementFilter>
        {
            new ElementMulticategoryFilter(categories is { Count: > 0 } ? categories : ModelCategoryIds(document)),
            new ElementIsElementTypeFilter(true)
        };
        filters.AddRange(options ?? OptionFilters(document));
        return new LogicalAndFilter(filters);
    }

    public static List<ElementFilter> OptionFilters(Document document)
    {
        return new FilteredElementCollector(document)
            .OfClass(typeof(DesignOption))
            .Cast<DesignOption>()
            .Where(option => !option.IsPrimary)
            .Select(option => (ElementFilter)new ElementDesignOptionFilter(option.Id, true))
            .ToList();
    }

    public static long[] CollectIds(Document document, ICollection<ElementId>? categories = null)
    {
        return new FilteredElementCollector(document)
            .WherePasses(ModelFilter(document, categories))
            .ToElementIds()
            .Select(id => id.ToLong())
            .ToArray();
    }

    public static DocumentVersion? VersionOf(Document document)
    {
        try
        {
            return Document.GetDocumentVersion(document) is { } version
                ? new DocumentVersion(version.VersionGUID.ToString(), version.NumberOfSaves)
                : null;
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Document version", document.Title);
            return null;
        }
    }

    public int ReadSlice(IReadOnlyList<long> ids, int start)
    {
        var watch = Stopwatch.StartNew();
        var index = start;
        while (index < ids.Count && index - start < SliceSize && watch.Elapsed < SliceTime)
        {
            if (document.GetElement(ids[index].ToElementId()) is { } element)
            {
                Read(element);
            }

            index++;
        }

        return index;
    }

    public void Read(Element element)
    {
        if (element is ElementType type)
        {
            ReadType(type);
            return;
        }

        if (element.Category is not { } category)
        {
            return;
        }

        var categoryId = category.Id.ToLong();
        if (!_modelCategories.Contains(categoryId))
        {
            return;
        }

        var typeId = element.GetTypeId();
        var typeValue = typeId == ElementId.InvalidElementId ? NoTypeBase + categoryId : typeId.ToLong();
        if (knownTypes.Add(typeValue))
        {
            var elementType = typeId == ElementId.InvalidElementId ? null : document.GetElement(typeId) as ElementType;
            Types.Add(new TypeRecord(typeValue, categoryId, category.Name,
                elementType?.FamilyName ?? NoType, elementType?.Name ?? NoType));
        }

        Elements.Add(new ElementRecord(element.Id.ToLong(), typeValue, element.LevelId.ToLong(),
            element.WorksetId.IntegerValue));
    }

    private void ReadType(ElementType type)
    {
        if (type.Category is not { } category)
        {
            return;
        }

        var categoryId = category.Id.ToLong();
        if (!_modelCategories.Contains(categoryId))
        {
            return;
        }

        var typeValue = type.Id.ToLong();
        knownTypes.Add(typeValue);
        Types.Add(new TypeRecord(typeValue, categoryId, category.Name, type.FamilyName, type.Name));
    }
}
