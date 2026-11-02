using System.Text.Json.Serialization;

namespace DevTools.Ipc;

public readonly record struct ProtocolRange(
    [property: JsonPropertyName("current")] int Current,
    [property: JsonPropertyName("min")] int Min)
{
    public bool Overlaps(ProtocolRange other) => Min <= other.Current && other.Min <= Current;
}

public sealed record ProtocolSender(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("protocol")] ProtocolRange Protocol);

public sealed record ProtocolPeer(string Name, ProtocolSender? Sender, string UpdateMessage)
{
    private ProtocolRange Range => Sender?.Protocol ?? default;

    public string Label => Sender is null ? Name : $"{Name} {Sender.Version}";

    public bool IsCompatible(ProtocolPeer other) => Range.Overlaps(other.Range);

    public string ExplainMismatch(ProtocolPeer other)
    {
        var older = other.Range.Current < Range.Min ? other : this;
        return $"{other.Label} is not compatible with {Label}. {older.UpdateMessage}";
    }
}
