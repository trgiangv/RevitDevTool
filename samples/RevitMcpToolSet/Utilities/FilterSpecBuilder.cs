using Autodesk.Revit.DB.Architecture;
using RevitMcpToolSet.Data;

namespace RevitMcpToolSet.Utilities;

internal static class FilterSpecBuilder
{
    private static readonly Dictionary<string, Type> ClassMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Wall"] = typeof(Wall),
        ["Floor"] = typeof(Floor),
        ["Ceiling"] = typeof(Ceiling),
        ["FamilyInstance"] = typeof(FamilyInstance),
        ["RoofBase"] = typeof(RoofBase),
        ["Group"] = typeof(Group),
        ["Room"] = typeof(Room),
        ["Area"] = typeof(Area),
        ["FamilySymbol"] = typeof(FamilySymbol),
        ["Material"] = typeof(Material),
        ["Grid"] = typeof(Grid),
        ["Level"] = typeof(Level),
        ["ViewSheet"] = typeof(ViewSheet),
        ["ViewSchedule"] = typeof(ViewSchedule),
        ["RevitLinkInstance"] = typeof(RevitLinkInstance),
    };

    public static FilteredElementCollector BuildCollector(
        Document doc,
        FilterSpec? filterSpec,
        bool selectedOnly,
        bool includeTypes,
        bool includeInstances)
    {
        FilterItem? viewItem = null;
        FilterItem? typeItem = null;
        if (filterSpec?.Filters is { Length: > 0 })
        {
            viewItem = filterSpec.Filters.FirstOrDefault(f =>
                f.Type.Equals(FilterTypes.View, StringComparison.OrdinalIgnoreCase));
            typeItem = filterSpec.Filters.FirstOrDefault(f =>
                f.Type.Equals(FilterTypes.ElementType, StringComparison.OrdinalIgnoreCase));
        }

        FilteredElementCollector collector;
        if (selectedOnly)
        {
            var uiDoc = Nice3point.Revit.Toolkit.RevitContext.ActiveUiDocument
                ?? throw new ModelContextProtocol.McpException("No active UI document for selection.");
            var selectedIds = uiDoc.Selection.GetElementIds();
            if (selectedIds.Count == 0)
                throw new ModelContextProtocol.McpException("No elements selected.");
            collector = new FilteredElementCollector(doc, selectedIds);
        }
        else if (viewItem is not null)
        {
            var view = ResolveView(doc, viewItem.ViewName);
            collector = new FilteredElementCollector(doc, view.Id);
        }
        else
        {
            collector = new FilteredElementCollector(doc);
        }

        var composite = BuildCompositeFilter(doc, filterSpec, excludeView: true, excludeElementType: true);
        if (composite is not null)
            collector = collector.WherePasses(composite);

        if (typeItem?.IsType is bool isType)
        {
            collector = isType
                ? collector.WhereElementIsElementType()
                : collector.WhereElementIsNotElementType();
        }

        if (!includeTypes && includeInstances)
            collector = collector.WhereElementIsNotElementType();
        else if (includeTypes && !includeInstances)
            collector = collector.WhereElementIsElementType();

        return collector;
    }

    private static ElementFilter? BuildCompositeFilter(
        Document doc,
        FilterSpec? filterSpec,
        bool excludeView,
        bool excludeElementType)
    {
        if (filterSpec?.Filters is not { Length: > 0 })
            return null;

        var subFilters = new List<ElementFilter>();
        foreach (var item in filterSpec.Filters)
        {
            if (excludeView && item.Type.Equals(FilterTypes.View, StringComparison.OrdinalIgnoreCase))
                continue;
            if (excludeElementType && item.Type.Equals(FilterTypes.ElementType, StringComparison.OrdinalIgnoreCase))
                continue;

            var built = BuildSingleFilter(doc, item);
            if (built is not null)
                subFilters.Add(built);
        }

        if (subFilters.Count == 0)
            return null;
        if (subFilters.Count == 1)
            return subFilters[0];

        return filterSpec.Logic.Equals("or", StringComparison.OrdinalIgnoreCase)
            ? new LogicalOrFilter(subFilters)
            : new LogicalAndFilter(subFilters);
    }

    private static ElementFilter? BuildSingleFilter(Document doc, FilterItem item)
    {
        return item.Type.ToLowerInvariant() switch
        {
            FilterTypes.Category => BuildCategoryFilter(doc, item),
            FilterTypes.ParameterString => BuildParameterStringFilter(doc, item),
            FilterTypes.ParameterNumeric => BuildParameterNumericFilter(doc, item),
            FilterTypes.ParameterHasValue => BuildParameterHasValueFilter(doc, item),
            FilterTypes.Level => BuildLevelFilter(doc, item),
            FilterTypes.Class => BuildClassFilter(item),
            FilterTypes.BoundingBox => BuildBoundingBoxFilter(item),
            FilterTypes.Workset => BuildWorksetFilter(doc, item),
            FilterTypes.Phase => BuildPhaseFilter(doc, item),
            FilterTypes.Exclusion => BuildExclusionFilter(item),
            _ => null,
        };
    }

    private static ElementFilter? BuildCategoryFilter(Document doc, FilterItem item)
    {
        if (item.Names is not { Length: > 0 })
            return null;

        var catIds = new List<ElementId>();
        foreach (var name in item.Names)
        {
            var cat = FindCategoryByName(doc, name);
            if (cat is not null)
                catIds.Add(cat.Id);
        }

        return catIds.Count == 0 ? null : new ElementMulticategoryFilter(catIds, item.Inverted);
    }

    private static ElementFilter? BuildParameterStringFilter(Document doc, FilterItem item)
    {
        if (string.IsNullOrWhiteSpace(item.ParameterName) || item.Value is null)
            return null;

        var paramId = FindParameterId(doc, item.ParameterName);
        if (paramId is null)
            return null;

        var value = item.Value.ToString() ?? "";
        var rule = (item.Operator ?? StringOperators.Equal).ToLowerInvariant() switch
        {
            StringOperators.Equal => ParameterFilterRuleFactory.CreateEqualsRule(paramId, value),
            StringOperators.NotEqual => ParameterFilterRuleFactory.CreateNotEqualsRule(paramId, value),
            StringOperators.Contains => ParameterFilterRuleFactory.CreateContainsRule(paramId, value),
            StringOperators.NotContains => ParameterFilterRuleFactory.CreateNotContainsRule(paramId, value),
            StringOperators.BeginsWith => ParameterFilterRuleFactory.CreateBeginsWithRule(paramId, value),
            StringOperators.EndsWith => ParameterFilterRuleFactory.CreateEndsWithRule(paramId, value),
            _ => null,
        };

        return rule is null ? null : new ElementParameterFilter(rule);
    }

    private static ElementFilter? BuildParameterNumericFilter(Document doc, FilterItem item)
    {
        if (string.IsNullOrWhiteSpace(item.ParameterName) || item.Value is null)
            return null;

        var paramId = FindParameterId(doc, item.ParameterName);
        if (paramId is null)
            return null;

        if (!TryToDouble(item.Value, out var numericValue))
            return null;

        var storageType = FindParameterStorageType(doc, item.ParameterName);
        var rule = (item.Operator ?? NumericOperators.Equal).ToLowerInvariant() switch
        {
            NumericOperators.Equal when storageType == StorageType.Integer =>
                ParameterFilterRuleFactory.CreateEqualsRule(paramId, (int)Math.Round(numericValue)),
            NumericOperators.NotEqual when storageType == StorageType.Integer =>
                ParameterFilterRuleFactory.CreateNotEqualsRule(paramId, (int)Math.Round(numericValue)),
            NumericOperators.GreaterThan when storageType == StorageType.Integer =>
                ParameterFilterRuleFactory.CreateGreaterRule(paramId, (int)Math.Round(numericValue)),
            NumericOperators.LessThan when storageType == StorageType.Integer =>
                ParameterFilterRuleFactory.CreateLessRule(paramId, (int)Math.Round(numericValue)),
            NumericOperators.GreaterOrEqual when storageType == StorageType.Integer =>
                ParameterFilterRuleFactory.CreateGreaterOrEqualRule(paramId, (int)Math.Round(numericValue)),
            NumericOperators.LessOrEqual when storageType == StorageType.Integer =>
                ParameterFilterRuleFactory.CreateLessOrEqualRule(paramId, (int)Math.Round(numericValue)),
            NumericOperators.Equal => ParameterFilterRuleFactory.CreateEqualsRule(paramId, numericValue, 1e-6),
            NumericOperators.NotEqual => ParameterFilterRuleFactory.CreateNotEqualsRule(paramId, numericValue, 1e-6),
            NumericOperators.GreaterThan => ParameterFilterRuleFactory.CreateGreaterRule(paramId, numericValue, 1e-6),
            NumericOperators.LessThan => ParameterFilterRuleFactory.CreateLessRule(paramId, numericValue, 1e-6),
            NumericOperators.GreaterOrEqual => ParameterFilterRuleFactory.CreateGreaterOrEqualRule(paramId, numericValue, 1e-6),
            NumericOperators.LessOrEqual => ParameterFilterRuleFactory.CreateLessOrEqualRule(paramId, numericValue, 1e-6),
            _ => null,
        };

        return rule is null ? null : new ElementParameterFilter(rule);
    }

    private static ElementFilter? BuildParameterHasValueFilter(Document doc, FilterItem item)
    {
        if (string.IsNullOrWhiteSpace(item.ParameterName) || item.HasValue is not bool hasValue)
            return null;

        var paramId = FindParameterId(doc, item.ParameterName);
        if (paramId is null)
            return null;

        var rule = hasValue
            ? ParameterFilterRuleFactory.CreateHasValueParameterRule(paramId)
            : ParameterFilterRuleFactory.CreateHasNoValueParameterRule(paramId);
        return new ElementParameterFilter(rule);
    }

    private static ElementFilter? BuildLevelFilter(Document doc, FilterItem item)
    {
        if (string.IsNullOrWhiteSpace(item.LevelName))
            return null;

        var level = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Levels)
            .WhereElementIsNotElementType()
            .Cast<Level>()
            .FirstOrDefault(l => l.Name.Equals(item.LevelName, StringComparison.OrdinalIgnoreCase));

        return level is null ? null : new ElementLevelFilter(level.Id);
    }

    private static ElementFilter? BuildClassFilter(FilterItem item)
    {
        if (item.ClassNames is not { Length: > 0 })
            return null;

        var types = new List<Type>();
        foreach (var name in item.ClassNames)
        {
            if (ClassMap.TryGetValue(name, out var type))
                types.Add(type);
        }

        if (types.Count == 0)
            return null;
        return types.Count == 1
            ? new ElementClassFilter(types[0])
            : new ElementMulticlassFilter(types);
    }

    private static ElementFilter BuildBoundingBoxFilter(FilterItem item)
    {
        if (item.MinPoint is not { Length: >= 3 } || item.MaxPoint is not { Length: >= 3 })
            throw new ModelContextProtocol.McpException("bounding_box filter requires min_point and max_point arrays of 3 coordinates.");

        var outline = new Outline(
            new XYZ(item.MinPoint[0], item.MinPoint[1], item.MinPoint[2]),
            new XYZ(item.MaxPoint[0], item.MaxPoint[1], item.MaxPoint[2]));

        return (item.Mode ?? BoundingBoxModes.Inside).Equals(BoundingBoxModes.Intersecting, StringComparison.OrdinalIgnoreCase)
            ? new BoundingBoxIntersectsFilter(outline)
            : new BoundingBoxIsInsideFilter(outline);
    }

    private static ElementFilter? BuildWorksetFilter(Document doc, FilterItem item)
    {
        if (string.IsNullOrWhiteSpace(item.WorksetName))
            return null;

        try
        {
            foreach (var workset in new FilteredWorksetCollector(doc).ToWorksets())
            {
                if (workset.Name.Equals(item.WorksetName, StringComparison.OrdinalIgnoreCase))
                    return new ElementWorksetFilter(workset.Id);
            }
        }
        catch
        {
            // Worksharing may be disabled.
        }

        return null;
    }

    private static ElementFilter? BuildPhaseFilter(Document doc, FilterItem item)
    {
        if (string.IsNullOrWhiteSpace(item.PhaseName))
            return null;

        ElementId? phaseId = null;
        foreach (Phase phase in doc.Phases)
        {
            if (phase.Name.Equals(item.PhaseName, StringComparison.OrdinalIgnoreCase))
            {
                phaseId = phase.Id;
                break;
            }
        }

        if (phaseId is null)
            return null;

        return new LogicalOrFilter(
        [
            new ElementPhaseStatusFilter(phaseId, ElementOnPhaseStatus.New),
            new ElementPhaseStatusFilter(phaseId, ElementOnPhaseStatus.Existing),
            new ElementPhaseStatusFilter(phaseId, ElementOnPhaseStatus.Demolished),
        ]);
    }

    private static ElementFilter? BuildExclusionFilter(FilterItem item)
    {
        if (item.ElementIds is not { Length: > 0 })
            return null;

        var ids = item.ElementIds.Select(id => id.ToElementId()).ToList();
        return new ExclusionFilter(ids);
    }

    private static View ResolveView(Document doc, string? viewName)
    {
        if (string.IsNullOrWhiteSpace(viewName))
        {
            var activeView = Nice3point.Revit.Toolkit.RevitContext.ActiveView
                ?? throw new ModelContextProtocol.McpException("No active view.");
            return activeView;
        }

        var view = new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .FirstOrDefault(v => v.Name.Equals(viewName, StringComparison.OrdinalIgnoreCase));

        return view ?? throw new ModelContextProtocol.McpException($"View '{viewName}' not found.");
    }

    private static Category? FindCategoryByName(Document doc, string name)
    {
        foreach (Category cat in doc.Settings.Categories)
        {
            if (cat.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return cat;
        }

        return null;
    }

    private static ElementId? FindParameterId(Document doc, string parameterName)
    {
        var target = parameterName.Trim();
        var sample = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .FirstElement();

        if (sample is not null)
        {
            var fromInstance = ScanParameters(sample, target);
            if (fromInstance is not null)
                return fromInstance;

            var typeId = sample.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                var typeElem = doc.GetElement(typeId);
                if (typeElem is not null)
                {
                    var fromType = ScanParameters(typeElem, target);
                    if (fromType is not null)
                        return fromType;
                }
            }
        }

        foreach (var sp in new FilteredElementCollector(doc).OfClass(typeof(SharedParameterElement)).Cast<SharedParameterElement>())
        {
            if (sp.Name.Equals(target, StringComparison.OrdinalIgnoreCase))
                return sp.Id;
        }

        return null;
    }

    private static ElementId? ScanParameters(Element element, string target)
    {
        foreach (Parameter param in element.Parameters)
        {
            if (param.Definition.Name.Equals(target, StringComparison.OrdinalIgnoreCase))
                return param.Id;
        }

        return null;
    }

    private static StorageType? FindParameterStorageType(Document doc, string parameterName)
    {
        var sample = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .FirstElement();
        if (sample is null)
            return null;

        var param = sample.LookupParameter(parameterName);
        return param?.StorageType;
    }

    private static bool TryToDouble(object value, out double result)
    {
        switch (value)
        {
            case double d:
                result = d;
                return true;
            case float f:
                result = f;
                return true;
            case int i:
                result = i;
                return true;
            case long l:
                result = l;
                return true;
            case string s when double.TryParse(s, out var parsed):
                result = parsed;
                return true;
            default:
                result = 0;
                return false;
        }
    }
}
