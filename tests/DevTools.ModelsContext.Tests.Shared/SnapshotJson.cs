using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Explorer;
using System.Text.Json;

namespace DevTools.ModelsContext.Tests.Shared;

public static class SnapshotJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string Serialize(DocumentSnapshot snapshot)
    {
        var document = new SnapshotDocument
        {
            Source = snapshot.Source.Value,
            GroupingDepth = snapshot.GroupingDepth,
            Types = snapshot.Types
                .Select(type => new TypeDocument
                {
                    TypeId = type.TypeId,
                    GroupId = snapshot.Groups[type.GroupIndex].GroupId,
                    Group = snapshot.Names[snapshot.Groups[type.GroupIndex].NameIndex],
                    ParentId = snapshot.Groups[type.GroupIndex].ParentId,
                    Family = snapshot.Names[type.FamilyNameIndex],
                    Type = snapshot.Names[type.TypeNameIndex]
                })
                .ToList(),
            Elements = snapshot.Rows
                .Select(row => new[] { row.Id, snapshot.Types[row.TypeIndex].TypeId, row.LevelId, row.WorksetId })
                .ToList()
        };
        return JsonSerializer.Serialize(document, Options);
    }

    public static DocumentSnapshot Deserialize(string json)
    {
        var document = JsonSerializer.Deserialize<SnapshotDocument>(json, Options)
                       ?? throw new InvalidDataException("The snapshot JSON is empty.");
        var builder = new DocumentSnapshotBuilder(new SourceId(document.Source), document.GroupingDepth);
        foreach (var type in document.Types)
        {
            builder.AddType(new TypeRecord(type.TypeId, type.GroupId, type.Group, type.Family, type.Type, type.ParentId));
        }

        foreach (var element in document.Elements)
        {
            builder.AddElement(new ElementRecord(element[0], element[1], element[2], element[3]));
        }

        return builder.Build();
    }

    public static void Save(DocumentSnapshot snapshot, string path)
    {
        File.WriteAllText(path, Serialize(snapshot));
    }

    public static DocumentSnapshot Load(string path)
    {
        return Deserialize(File.ReadAllText(path));
    }

    private sealed class SnapshotDocument
    {
        public string Source { get; set; } = string.Empty;

        public int GroupingDepth { get; set; } = TreeSchema.MaxGroupingDepth;

        public List<TypeDocument> Types { get; set; } = [];

        public List<long[]> Elements { get; set; } = [];
    }

    private sealed class TypeDocument
    {
        public long TypeId { get; set; }

        public long GroupId { get; set; }

        public string Group { get; set; } = string.Empty;

        public long ParentId { get; set; }

        public string Family { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;
    }
}
