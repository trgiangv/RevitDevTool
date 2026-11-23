using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Tests;

internal static class TestModel
{
    public const long Ducts = -2008000;
    public const long Walls = -2000011;
    public const long Doors = -2000023;

    private static readonly Lazy<FakeModelData> Small = new(() => FakeModelGenerator.Generate(new FakeModelOptions
    {
        CurrentElements = 5_000,
        LinkElements = 2_000
    }));

    public static FakeModelData SmallData => Small.Value;

    public static DocumentSnapshot Snapshot(DocumentKey key)
    {
        return FakeModelGenerator.BuildSnapshot(SmallData.SourceOf(key));
    }

    public static VisibilityPlanner Planner(FakeModelData data, params DocumentKey[] loaded)
    {
        var snapshots = new Dictionary<SourceId, DocumentSnapshot>();
        foreach (var key in loaded)
        {
            var source = data.Document(key).Source;
            if (!snapshots.ContainsKey(source))
            {
                snapshots[source] = FakeModelGenerator.BuildSnapshot(data.Sources[source]);
            }
        }

        return new VisibilityPlanner(
            key => data.Documents.FirstOrDefault(document => document.Key == key),
            key => snapshots.TryGetValue(data.Document(key).Source, out var snapshot)
                ? new DocumentScope(data.Document(key), key.ToString(), snapshot, null)
                : null);
    }

    public static FakeModelData WithoutLinkFilters()
    {
        return SmallData with
        {
            Documents = SmallData.Documents
                .Select(document => document.Key.IsCurrentModel
                    ? document
                    : document with { Capabilities = document.Capabilities & ~DocumentCapabilities.LinkFilters })
                .ToList()
        };
    }

    public static TreeRow GroupRow(DocumentSnapshot snapshot, DocumentKey key, long groupId)
    {
        var index = snapshot.GroupIndexOf(groupId);
        return new TreeRow(TreeRowKind.Group, snapshot.GroupName(index), document: key,
            path: RowPath.ForGroup(index));
    }
}
