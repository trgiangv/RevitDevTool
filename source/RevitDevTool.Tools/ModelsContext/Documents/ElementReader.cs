using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Documents;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Filters;
using RevitDevTool.Tools.ModelsContext.Pane;
using System.Diagnostics;
using DocumentVersion = DevTools.ModelsContext.Documents.DocumentVersion;
using ElementRecord = DevTools.ModelsContext.Documents.ElementRecord;

namespace RevitDevTool.Tools.ModelsContext.Documents;

internal sealed class ElementReader(Document document, HashSet<long> knownTypes, HashSet<long>? listedCategories = null)
{
    public const int SliceSize = 5000;
    public static readonly TimeSpan SliceTime = TimeSpan.FromMilliseconds(30);

    private const long NoTypeBase = -1_000_000_000_000L;
    private const long OtherCategoryBase = -10_000_000_000_000L;
    private const long CategorySpan = 10_000_000L;
    private const string NoType = "No type";

    private readonly HashSet<long> _listedCategories = listedCategories ?? ListedSet(document);
    private readonly Dictionary<long, ElementType?> _types = [];

    public List<TypeRecord> Types { get; } = [];

    public List<ElementRecord> Elements { get; } = [];

    public static List<ElementId> ListedCategoryIds(Document document)
    {
        var ids = new List<ElementId>();
        var inLink = document.IsLinked;
        foreach (Category category in document.Settings.Categories)
        {
            var parent = category.BuiltInCategory.ToString();
            if (!CategoryRule.Lists(KindOf(category), category.IsVisibleInUI, parent, inLink))
            {
                continue;
            }

            ids.Add(category.Id);
            if (!SubcategoryRule.ListsChildrenOf(parent))
            {
                continue;
            }

            foreach (Category subcategory in category.SubCategories)
            {
                if (CategoryRule.ListsSubcategory(KindOf(subcategory), subcategory.IsVisibleInUI, parent,
                        subcategory.BuiltInCategory.ToString(), inLink))
                {
                    ids.Add(subcategory.Id);
                }
            }
        }

        var detailGroups = new ElementId(BuiltInCategory.OST_IOSDetailGroups);
        if (CategoryRule.Lists(CategoryKind.Other, true, CategoryRule.DetailGroups, inLink) && !ids.Contains(detailGroups))
        {
            ids.Add(detailGroups);
        }

        return ids;
    }

    public static HashSet<long> ListedSet(Document document)
    {
        return [.. ListedCategoryIds(document).Select(id => id.ToLong())];
    }

    private static CategoryKind KindOf(Category category)
    {
        return category.CategoryType switch
        {
            CategoryType.Model => CategoryKind.Model,
            CategoryType.Annotation => CategoryKind.Annotation,
            _ => CategoryKind.Other
        };
    }

    public static List<ElementId> OwnedCategoryIds(Document document)
    {
        var all = ListedCategoryIds(document);
        var subcategories = all.Where(id => Category.GetCategory(document, id)?.Parent is not null).ToList();
        var owned = new HashSet<ElementId>(all.Except(subcategories));
        if (subcategories.Count > 0)
        {
            foreach (var element in document.CollectElements()
                         .WherePasses(new ElementMulticategoryFilter(subcategories))
                         .WhereElementIsNotElementType())
            {
                if (element.Category is { } category)
                {
                    owned.Add(category.Id);
                }
            }
        }

        return [.. owned];
    }

    public static long ParentOf(Category category)
    {
        return category.Parent?.Id.ToLong() ?? GroupRow.TopLevel;
    }

    public static ElementFilter ModelFilter(
        Document document,
        ICollection<ElementId>? categories = null,
        IReadOnlyList<ElementFilter>? options = null)
    {
        var filters = new List<ElementFilter> { AnyOptionFilter(document, categories), FreeFilter() };
        filters.AddRange(options ?? OptionFilters(document));
        return new LogicalAndFilter(filters);
    }

    public static ElementFilter AnyOptionFilter(Document document, ICollection<ElementId>? categories = null)
    {
        var filters = new List<ElementFilter>
        {
            new ElementMulticategoryFilter(categories is { Count: > 0 } ? categories : ListedCategoryIds(document)),
            new ElementIsElementTypeFilter(true)
        };
        if (document.IsLinked)
        {
            filters.Add(FreeFilter());
        }

        return new LogicalAndFilter(filters);
    }

    private static ElementOwnerViewFilter FreeFilter()
    {
        return new ElementOwnerViewFilter(ElementId.InvalidElementId);
    }

    public static HashSet<long> ViewOwnedIds(Document document, ICollection<ElementId>? categories = null)
    {
        if (document.IsLinked)
        {
            return [];
        }

        return
        [
            .. document.CollectElements()
                .WherePasses(AnyOptionFilter(document, categories))
                .WherePasses(new ElementOwnerViewFilter(ElementId.InvalidElementId, true))
                .ToElementIds()
                .Select(id => id.ToLong())
        ];
    }

    public static List<ElementFilter> OptionFilters(Document document)
    {
        return SecondaryOptions(document)
            .Select(option => (ElementFilter)new ElementDesignOptionFilter(option, true))
            .ToList();
    }

    public static long[] CollectIds(Document document, ICollection<ElementId>? categories = null)
    {
        return document.CollectElements()
            .WherePasses(ModelFilter(document, categories))
            .ToElementIds()
            .Select(id => id.ToLong())
            .ToArray();
    }

    public static long[] CollectAnyOption(Document document, ICollection<ElementId>? categories = null)
    {
        return document.CollectElements()
            .WherePasses(AnyOptionFilter(document, categories))
            .ToElementIds()
            .Select(id => id.ToLong())
            .ToArray();
    }

    public static CandidateSet CandidatesOf(Document document, ICollection<ElementId>? categories = null)
    {
        return new CandidateSet(CollectAnyOption(document, categories), SecondaryIds(document, categories),
            ViewOwnedIds(document, categories));
    }

    public static CandidateSet CandidatesAmong(Document document, ICollection<ElementId> ids)
    {
        if (ids.Count == 0)
        {
            return new CandidateSet([], [], []);
        }

        var inModel = document.CollectElements(ids)
            .WherePasses(AnyOptionFilter(document))
            .ToElementIds()
            .Select(id => id.ToLong())
            .ToArray();
        var secondary = SecondaryIds(document);
        secondary.IntersectWith(inModel);
        var owned = ViewOwnedIds(document);
        owned.IntersectWith(inModel);
        return new CandidateSet(inModel, secondary, owned);
    }

    public static HashSet<long> SecondaryIds(Document document, ICollection<ElementId>? categories = null)
    {
        var options = SecondaryOptions(document)
            .Select(option => (ElementFilter)new ElementDesignOptionFilter(option))
            .ToList();
        if (options.Count == 0)
        {
            return [];
        }

        return
        [
            .. document.CollectElements()
                .WherePasses(AnyOptionFilter(document, categories))
                .WherePasses(options.Count == 1 ? options[0] : new LogicalOrFilter(options))
                .ToElementIds()
                .Select(id => id.ToLong())
        ];
    }

    public static List<long> OptionMemberIds(Document document)
    {
        var options = document.CollectElements()
            .OfClass<DesignOption>()
            .Select(option => (ElementFilter)new ElementDesignOptionFilter(option.Id))
            .ToList();
        if (options.Count == 0)
        {
            return [];
        }

        return document.CollectElements()
            .WherePasses(AnyOptionFilter(document))
            .WherePasses(options.Count == 1 ? options[0] : new LogicalOrFilter(options))
            .ToElementIds()
            .Select(id => id.ToLong())
            .ToList();
    }

    private static IEnumerable<ElementId> SecondaryOptions(Document document)
    {
        return document.CollectElements()
            .OfClass<DesignOption>()
            .Cast<DesignOption>()
            .Where(option => !option.IsPrimary)
            .Select(option => option.Id);
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

        if (element.Category is not { } category || (document.IsLinked && element.OwnerViewId != ElementId.InvalidElementId))
        {
            return;
        }

        var categoryId = category.Id.ToLong();
        if (!_listedCategories.Contains(categoryId))
        {
            return;
        }

        var typeId = element.GetTypeId();
        var elementType = TypeOf(typeId);
        var typeValue = TypeValueOf(typeId, elementType, categoryId);
        if (knownTypes.Add(typeValue))
        {
            Types.Add(new TypeRecord(typeValue, categoryId, category.Name,
                elementType?.FamilyName ?? NoType, elementType?.Name ?? NoType, ParentOf(category)));
        }

        Elements.Add(new ElementRecord(element.Id.ToLong(), typeValue, element.LevelId.ToLong(),
            element.WorksetId.IntegerValue, element.DesignOption is { IsPrimary: false }, ElementDrawing.DrawOf(element),
            ElementDrawing.OwnerOf(element)));
    }

    private static long TypeValueOf(ElementId typeId, ElementType? type, long categoryId)
    {
        if (typeId == ElementId.InvalidElementId)
        {
            return NoTypeBase + categoryId;
        }

        return type?.Category is { } typeCategory && typeCategory.Id.ToLong() != categoryId
            ? OtherCategoryBase - typeId.ToLong() * CategorySpan - Math.Abs(categoryId) % CategorySpan
            : typeId.ToLong();
    }

    private ElementType? TypeOf(ElementId typeId)
    {
        if (typeId == ElementId.InvalidElementId)
        {
            return null;
        }

        var key = typeId.ToLong();
        if (!_types.TryGetValue(key, out var type))
        {
            type = document.GetElement(typeId) as ElementType;
            _types.Add(key, type);
        }

        return type;
    }

    private void ReadType(ElementType type)
    {
        if (type.Category is not { } category)
        {
            return;
        }

        var categoryId = category.Id.ToLong();
        if (!_listedCategories.Contains(categoryId))
        {
            return;
        }

        var typeValue = type.Id.ToLong();
        knownTypes.Add(typeValue);
        Types.Add(new TypeRecord(typeValue, categoryId, category.Name, type.FamilyName, type.Name, ParentOf(category)));
    }
}
