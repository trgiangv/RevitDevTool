using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace DevTools.ModelsContext.Tests.Shared;

public static class FakeParameters
{
    public const string LengthSpec = "spec:length";
    public const string TextSpec = "spec:text";
    public const string IntegerSpec = "spec:int";
    public const string AngleSpec = "spec:angle";
    public const string LeftDoors = "Doors Family 1";
    public const string RightDoors = "Doors Family 2";

    public static readonly ParameterKey Mark = ParameterKey.BuiltIn("Mark", "ALL_MODEL_MARK");
    public static readonly ParameterKey Comments = ParameterKey.BuiltIn("Comments", "ALL_MODEL_INSTANCE_COMMENTS");
    public static readonly ParameterKey Width = ParameterKey.Named("Width", LengthSpec);
    public static readonly ParameterKey Level = ParameterKey.BuiltIn("Level", "LEVEL_PARAM");
    public static readonly ParameterKey TextCode = ParameterKey.Named("Code", TextSpec);
    public static readonly ParameterKey IntegerCode = ParameterKey.Named("Code", IntegerSpec);
    public static readonly ParameterKey SwingAngle = ParameterKey.Named("Swing Angle", AngleSpec);
    public static readonly ParameterKey Flag = ParameterKey.BuiltIn("Flag", "FLAG_PARAM");
    public static readonly ParameterKey Function = ParameterKey.BuiltIn("Function", "FUNCTION_PARAM");
    public static readonly ParameterKey FireRating = ParameterKey.BuiltIn("Fire Rating", "FIRE_RATING");
    public static readonly ParameterKey SystemType = ParameterKey.BuiltIn("System Type", "SYSTEM_TYPE");
    public static readonly ParameterKey Insulation = ParameterKey.Named("Insulation", TextSpec);

    private static readonly ConditionalWeakTable<DocumentSnapshot, IReadOnlyCollection<long>> SwingIdsBySnapshot = new();

    private static readonly Dictionary<string, long> SwingAngleIds = new(StringComparer.Ordinal)
    {
        [LeftDoors] = 376576,
        [RightDoors] = 376590
    };

    private static readonly HashSet<string> SizedGroups = ["Ducts", "Pipes", "Cable Trays", "Conduits"];
    private static readonly HashSet<string> RatedGroups = ["Walls", "Doors"];
    private static readonly string[] FireRatings = ["", "1h", "2h", "3h"];
    private static readonly string[] Systems = ["Supply", "Return", "Exhaust"];

    public static IReadOnlyList<ParameterInfo> Catalog(FakeSource source)
    {
        var groups = source.Types.Select(type => type.GroupName).ToHashSet(StringComparer.Ordinal);
        var all = CategoriesOf(source, _ => true);
        var isCurrent = source.Id == FakeModelGenerator.CurrentSource;
        var catalog = new List<ParameterInfo>
        {
            BuiltIn(Mark, ParameterStorage.String, "Text"),
            BuiltIn(Comments, ParameterStorage.String, "Text"),
            BuiltIn(Level, ParameterStorage.ElementId, null),
            new(Flag, Flag.Name, ValueKind.YesNo, ParameterStorage.Integer, null, "Yes/No", ParameterOrigin.BuiltIn,
                ParameterBinding.Instance),
            isCurrent
                ? Named(TextCode, ParameterStorage.String, "Text", ParameterOrigin.Project)
                : Named(IntegerCode, ParameterStorage.Integer, "Integer", ParameterOrigin.Project)
        };
        for (var index = 0; index < catalog.Count; index++)
        {
            catalog[index] = catalog[index] with { Categories = all };
        }

        if (groups.Contains("Doors"))
        {
            catalog.Add(Named(SwingAngle, ParameterStorage.Double, "Angle", ParameterOrigin.Family) with
            {
                Categories = CategoriesOf(source, name => name == "Doors"),
                Families = [LeftDoors, RightDoors],
                Group = "Other"
            });
        }

        if (groups.Contains("Walls"))
        {
            catalog.Add(BuiltIn(Function, ParameterStorage.String, "Text") with
            {
                Bindings = ParameterBinding.Type,
                Categories = CategoriesOf(source, name => name == "Walls")
            });
        }

        if (groups.Overlaps(RatedGroups))
        {
            catalog.Add(BuiltIn(FireRating, ParameterStorage.String, "Text") with
            {
                Bindings = ParameterBinding.Type,
                Categories = CategoriesOf(source, RatedGroups.Contains)
            });
        }

        if (groups.Contains("Ducts"))
        {
            var ducts = CategoriesOf(source, name => name == "Ducts");
            catalog.Add(BuiltIn(SystemType, ParameterStorage.String, "Text") with { Categories = ducts });
            catalog.Add(Named(Insulation, ParameterStorage.String, "Text", ParameterOrigin.Project) with
            {
                Categories = ducts
            });
        }

        if (groups.Overlaps(SizedGroups))
        {
            var width = isCurrent
                ? Named(Width, ParameterStorage.Double, "Length", ParameterOrigin.Project)
                : Named(Width, ParameterStorage.Double, "Length", ParameterOrigin.Family) with
                {
                    Bindings = ParameterBinding.Type
                };
            catalog.Add(width with { Categories = CategoriesOf(source, SizedGroups.Contains) });
        }

        return catalog;
    }

    public static IReadOnlyList<ParameterInfo> Extra(FakeSource source, int count, int start)
    {
        var all = CategoriesOf(source, _ => true);
        return Enumerable.Range(start, count)
            .Select(index =>
            {
                var name = string.Format(CultureInfo.InvariantCulture, "Param {0:D4}", index);
                return (index % 3 == 0
                    ? Named(ParameterKey.Named(name, LengthSpec), ParameterStorage.Double, "Length", ParameterOrigin.Shared)
                    : Named(ParameterKey.Named(name, TextSpec), ParameterStorage.String, "Text", ParameterOrigin.Project)) with
                {
                    Categories = all
                };
            })
            .ToList();
    }

    public static ValueKind KindOf(ParameterKey key)
    {
        return key.Name switch
        {
            "Width" or "Swing Angle" => ValueKind.Number,
            "Level" => ValueKind.Reference,
            "Flag" => ValueKind.YesNo,
            _ => ValueKind.Text
        };
    }

    public static IReadOnlyCollection<long> IdsOf(DocumentSnapshot snapshot, ParameterKey key)
    {
        return key == SwingAngle
            ? SwingIdsBySnapshot.GetValue(snapshot,
                rows => rows.Rows.Select(row => IdOf(rows, row, SwingAngle)).OfType<long>().Distinct().ToList())
            : [0];
    }

    public static long? IdOf(DocumentSnapshot snapshot, ElementRow row, ParameterKey key)
    {
        if (key != SwingAngle)
        {
            return 0;
        }

        return SwingAngleIds.TryGetValue(snapshot.FamilyName(row.TypeIndex), out var id)
               && snapshot.GroupName(row.GroupIndex) == "Doors"
            ? id
            : null;
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
            case "Swing Angle" when IdOf(snapshot, row, key) is not null:
                value = row.Id % 4 == 0 ? string.Empty : (row.Id % 90).ToString(CultureInfo.InvariantCulture);
                return true;
            case "Function" when snapshot.GroupName(row.GroupIndex) == "Walls":
                value = row.Id % 3 == 0 ? "Interior" : "Exterior";
                return true;
            case "Fire Rating" when RatedGroups.Contains(snapshot.GroupName(row.GroupIndex)):
                value = FireRatings[row.Id % FireRatings.Length];
                return true;
            case "System Type" when snapshot.GroupName(row.GroupIndex) == "Ducts":
                value = Systems[row.Id % Systems.Length];
                return true;
            case "Insulation" when snapshot.GroupName(row.GroupIndex) == "Ducts" && row.Id % 2 == 0:
                value = "Fiberglass";
                return true;
            case "Flag":
                value = ValueKinds.YesNoText((int)(row.Id % 2));
                return true;
            case "Level":
                value = levels.TryGetValue(row.LevelId, out var name) ? name : string.Empty;
                return true;
            default:
                value = string.Empty;
                return false;
        }
    }

    private static CategoryIdSet CategoriesOf(FakeSource source, Func<string, bool> include)
    {
        return CategoryIdSet.Of(source.Types.Where(type => include(type.GroupName)).Select(type => type.GroupId));
    }

    private static ParameterInfo BuiltIn(ParameterKey key, ParameterStorage storage, string? label)
    {
        return new ParameterInfo(key, key.Name, ValueKinds.Infer(storage, false, false, false), storage, null, label,
            ParameterOrigin.BuiltIn, ParameterBinding.Instance)
        {
            Group = "Identity Data"
        };
    }

    private static ParameterInfo Named(ParameterKey key, ParameterStorage storage, string label, ParameterOrigin origin)
    {
        var measured = storage == ParameterStorage.Double;
        return new ParameterInfo(key, key.Name, ValueKinds.Infer(storage, false, measured, false),
            storage, key.DataType, label, origin, ParameterBinding.Instance)
        {
            Discipline = measured ? "Common" : null,
            Group = measured ? "Dimensions" : "Identity Data"
        };
    }
}
