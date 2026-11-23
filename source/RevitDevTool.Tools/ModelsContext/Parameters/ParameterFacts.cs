using DevTools.ModelsContext.Parameters;

namespace RevitDevTool.Tools.ModelsContext.Parameters;

internal sealed class ParameterFacts
{
    public bool IsFilterable { get; set; }

    public StorageType? Storage { get; set; }

    public ForgeTypeId? Spec { get; set; }

    public ParameterBinding Observed { get; set; }

    public bool NamesChecked { get; set; }

    public bool HasNamedValues { get; set; }

    public bool GroupChecked { get; set; }

    public ForgeTypeId? Group { get; set; }

    public HashSet<long> Categories { get; } = [];

    public HashSet<long> Verified { get; } = [];

    public HashSet<string> Families { get; } = new(StringComparer.Ordinal);
}
