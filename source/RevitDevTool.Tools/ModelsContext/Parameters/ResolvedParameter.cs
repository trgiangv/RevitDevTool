using DevTools.ModelsContext.Filters;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Filters;

namespace RevitDevTool.Tools.ModelsContext.Parameters;

internal sealed record ResolvedParameter(
    long Id,
    ParameterKey Key,
    StorageType Storage,
    string? SpecTypeId,
    TypePresence Types,
    IReadOnlyCollection<long> Lacking)
{
    public HeldMeaning Held { get; init; } = HeldMeaning.Value;

    public SilentElements Silent { get; init; } = SilentElements.None;

    public bool SilentChecked { get; init; }

    public ElementId ElementId => Id.ToElementId();

    public ForgeTypeId? Spec => SpecTypeId is null ? null : new ForgeTypeId(SpecTypeId);

    public bool IsYesNo => SpecInfo.IsYesNo(Spec);
}

internal enum HeldMeaning
{
    Value,
    Empty,
    Absent
}

internal sealed record SilentElements(IReadOnlyCollection<long> Filled, IReadOnlyCollection<long> Empty)
{
    public static SilentElements None { get; } = new([], []);

    public int Count => Filled.Count + Empty.Count;

    public IReadOnlyCollection<long> All => [.. Filled, .. Empty];
}
