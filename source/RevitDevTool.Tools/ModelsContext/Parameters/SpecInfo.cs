using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Pane;

namespace RevitDevTool.Tools.ModelsContext.Parameters;

internal static class SpecInfo
{
    public static ParameterStorage StorageOf(StorageType storage)
    {
        return storage switch
        {
            StorageType.Integer => ParameterStorage.Integer,
            StorageType.Double => ParameterStorage.Double,
            StorageType.String => ParameterStorage.String,
            StorageType.ElementId => ParameterStorage.ElementId,
            _ => ParameterStorage.None
        };
    }

    public static ForgeTypeId? SpecOf(Definition? definition)
    {
        try
        {
            return definition?.GetDataType() is { } spec && !string.IsNullOrEmpty(spec.TypeId) ? spec : null;
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Parameter spec", definition?.Name);
            return null;
        }
    }

    public static string? DataTypeOf(ForgeTypeId? spec, StorageType? storage)
    {
        return ParameterDataTypes.Of(spec?.TypeId, storage?.ToString());
    }

    public static bool Fits(ParameterKey key, ForgeTypeId? spec, StorageType? storage)
    {
        return ParameterDataTypes.Fits(key, spec?.TypeId, storage?.ToString());
    }

    public static bool IsYesNo(ForgeTypeId? spec)
    {
        return spec is not null && spec.Equals(SpecTypeId.Boolean.YesNo);
    }

    public static bool IsMeasurable(ForgeTypeId? spec)
    {
        try
        {
            return spec is not null && spec.IsMeasurableSpec;
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Measurable spec", spec?.TypeId);
            return false;
        }
    }

    public static string? DisciplineLabel(ForgeTypeId? spec)
    {
        if (!IsMeasurable(spec))
        {
            return null;
        }

        try
        {
            return LabelUtils.GetLabelForDiscipline(UnitUtils.GetDiscipline(spec));
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Discipline label", spec?.TypeId);
            return null;
        }
    }

    public static ForgeTypeId? GroupOf(Definition? definition)
    {
        try
        {
            return definition?.GetGroupTypeId() is { } group && !string.IsNullOrEmpty(group.TypeId) ? group : null;
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Parameter group", definition?.Name);
            return null;
        }
    }

    public static string? GroupLabel(ForgeTypeId? group)
    {
        if (group is null)
        {
            return null;
        }

        try
        {
            return LabelUtils.GetLabelForGroup(group) is { Length: > 0 } label ? label : null;
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Group label", group.TypeId);
            return null;
        }
    }

    public static string? SpecLabel(ForgeTypeId? spec)
    {
        try
        {
            return spec?.ToSpecLabel();
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Spec label", spec?.TypeId);
            return null;
        }
    }
}
