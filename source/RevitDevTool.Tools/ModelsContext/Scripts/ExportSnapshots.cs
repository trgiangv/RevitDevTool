using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DevTools.ModelsContext.Documents;

[Transaction(TransactionMode.ReadOnly)]
public class Command : IExternalCommand
{
    private const long NoTypeBase = -1_000_000_000_000L;
    private const string NoType = "No type";

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var model = commandData.Application.ActiveUIDocument.Document;
        var folder = Path.Combine(Path.GetTempPath(), "ModelContextSnapshots",
            DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        var written = new List<string>();
        var modelId = string.IsNullOrEmpty(model.PathName) ? "unsaved:" + model.Title : model.PathName;
        written.Add(Write(folder, "current", "model:" + modelId, model));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var instance in new FilteredElementCollector(model).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>())
        {
            var linkDocument = instance.GetLinkDocument();
            if (linkDocument == null || string.IsNullOrEmpty(linkDocument.PathName))
            {
                continue;
            }

            var source = "link:" + linkDocument.PathName.ToLowerInvariant();
            if (seen.Add(source))
            {
                written.Add(Write(folder, "link-" + seen.Count, source, linkDocument));
            }
        }

        message = string.Join(Environment.NewLine, written);
        return Result.Succeeded;
    }

    private static string Write(string folder, string name, string source, Document document)
    {
        var categories = ListedCategoryIds(document);
        var types = new StringBuilder();
        var rows = new StringBuilder();
        var knownTypes = new HashSet<long>();
        foreach (var element in new FilteredElementCollector(document).WherePasses(ModelFilter(document, categories)))
        {
            var category = element.Category;
            if (category == null)
            {
                continue;
            }

            var typeId = element.GetTypeId();
            var typeValue = typeId == ElementId.InvalidElementId ? NoTypeBase + Value(category.Id) : Value(typeId);
            if (knownTypes.Add(typeValue))
            {
                AppendType(types, document, category, typeId, typeValue);
            }

            AppendRow(rows, element, typeValue);
        }

        var json = "{\"source\":" + Quote(source) + ",\"groupingDepth\":3,\"types\":[" + types + "],\"elements\":[" + rows + "]}";
        var path = Path.Combine(folder, name + ".snapshot.json");
        File.WriteAllText(path, json, new UTF8Encoding(false));
        return path;
    }

    private static List<ElementId> ListedCategoryIds(Document document)
    {
        var categories = new List<ElementId>();
        var inLink = document.IsLinked;
        foreach (Category category in document.Settings.Categories)
        {
            var parent = category.BuiltInCategory.ToString();
            if (!CategoryRule.Lists(KindOf(category), category.IsVisibleInUI, parent, inLink))
            {
                continue;
            }

            categories.Add(category.Id);
            if (!SubcategoryRule.ListsChildrenOf(parent))
            {
                continue;
            }

            foreach (Category subcategory in category.SubCategories)
            {
                if (CategoryRule.ListsSubcategory(KindOf(subcategory), subcategory.IsVisibleInUI, parent,
                        subcategory.BuiltInCategory.ToString(), inLink))
                {
                    categories.Add(subcategory.Id);
                }
            }
        }

        var detailGroups = new ElementId(BuiltInCategory.OST_IOSDetailGroups);
        if (CategoryRule.Lists(CategoryKind.Other, true, CategoryRule.DetailGroups, inLink) && !categories.Contains(detailGroups))
        {
            categories.Add(detailGroups);
        }

        return categories;
    }

    private static CategoryKind KindOf(Category category)
    {
        switch (category.CategoryType)
        {
            case CategoryType.Model:
                return CategoryKind.Model;
            case CategoryType.Annotation:
                return CategoryKind.Annotation;
            default:
                return CategoryKind.Other;
        }
    }

    private static long ParentOf(Category category)
    {
        return category.Parent != null ? Value(category.Parent.Id) : 0;
    }

    private static ElementFilter ModelFilter(Document document, List<ElementId> categories)
    {
        var filters = new List<ElementFilter>
        {
            new ElementMulticategoryFilter(categories),
            new ElementIsElementTypeFilter(true),
            new ElementOwnerViewFilter(ElementId.InvalidElementId)
        };
        filters.AddRange(new FilteredElementCollector(document)
            .OfClass(typeof(DesignOption))
            .Cast<DesignOption>()
            .Where(option => !option.IsPrimary)
            .Select(option => (ElementFilter)new ElementDesignOptionFilter(option.Id, true)));
        return new LogicalAndFilter(filters);
    }

    private static void AppendType(StringBuilder types, Document document, Category category, ElementId typeId,
        long typeValue)
    {
        var elementType = typeId == ElementId.InvalidElementId ? null : document.GetElement(typeId) as ElementType;
        AppendSeparator(types);
        types.Append("{\"typeId\":").Append(typeValue.ToString(CultureInfo.InvariantCulture))
            .Append(",\"groupId\":").Append(Value(category.Id).ToString(CultureInfo.InvariantCulture))
            .Append(",\"group\":").Append(Quote(category.Name))
            .Append(",\"parentId\":").Append(ParentOf(category).ToString(CultureInfo.InvariantCulture))
            .Append(",\"family\":").Append(Quote(elementType != null ? elementType.FamilyName : NoType))
            .Append(",\"type\":").Append(Quote(elementType != null ? elementType.Name : NoType))
            .Append('}');
    }

    private static void AppendRow(StringBuilder rows, Element element, long typeValue)
    {
        AppendSeparator(rows);
        rows.Append('[').Append(Value(element.Id).ToString(CultureInfo.InvariantCulture))
            .Append(',').Append(typeValue.ToString(CultureInfo.InvariantCulture))
            .Append(',').Append(Value(element.LevelId).ToString(CultureInfo.InvariantCulture))
            .Append(',').Append(element.WorksetId.IntegerValue.ToString(CultureInfo.InvariantCulture))
            .Append(']');
    }

    private static void AppendSeparator(StringBuilder builder)
    {
        if (builder.Length > 0)
        {
            builder.Append(',');
        }
    }

    private static long Value(ElementId id)
    {
        return long.Parse(id.ToString(), CultureInfo.InvariantCulture);
    }

    private static string Quote(string text)
    {
        var builder = new StringBuilder("\"");
        foreach (var c in text ?? string.Empty)
        {
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                default:
                    if (c < ' ')
                    {
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        return builder.Append('"').ToString();
    }
}
