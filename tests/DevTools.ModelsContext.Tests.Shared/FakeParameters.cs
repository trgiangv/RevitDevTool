using DevTools.ModelsContext.Model;
using System.Globalization;

namespace DevTools.ModelsContext.Tests.Shared;

public static class FakeParameters
{
    public static readonly ParameterKey Mark = new("Mark", "ALL_MODEL_MARK");
    public static readonly ParameterKey Comments = new("Comments", "ALL_MODEL_INSTANCE_COMMENTS");
    public static readonly ParameterKey Width = new("Width");
    public static readonly ParameterKey Level = new("Level", "LEVEL_PARAM");

    private static readonly HashSet<string> SizedGroups = ["Ducts", "Pipes", "Cable Trays", "Conduits"];

    public static IReadOnlyList<ParameterInfo> Catalog(FakeSource source)
    {
        var groups = source.Types.Select(type => type.GroupName).ToHashSet(StringComparer.Ordinal);
        var catalog = new List<ParameterInfo>
        {
            new(Mark, "Mark", ValueKind.Text),
            new(Comments, "Comments", ValueKind.Text),
            new(Level, "Level", ValueKind.Text)
        };
        if (groups.Overlaps(SizedGroups))
        {
            catalog.Add(new ParameterInfo(Width, "Width", ValueKind.Number));
        }

        return catalog;
    }

    public static bool TryGet(
        DocumentSnapshot snapshot,
        ElementRow row,
        ParameterKey key,
        IReadOnlyDictionary<long, string> levels,
        out string value)
    {
        switch (key.Name)
        {
            case "Mark":
                value = $"M-{row.Id % 1000:D3}";
                return true;
            case "Comments":
                value = row.Id % 3 == 0 ? "Check" : string.Empty;
                return true;
            case "Width" when SizedGroups.Contains(snapshot.GroupName(row.GroupIndex)):
                value = ((row.Id % 20 + 1) * 50).ToString(CultureInfo.InvariantCulture);
                return true;
            case "Level":
                value = levels.TryGetValue(row.LevelId, out var name) ? name : string.Empty;
                return true;
            default:
                value = string.Empty;
                return false;
        }
    }
}
