namespace DevTools.ModelsContext.Model;

public enum LinkStatus
{
    Unloaded,
    Reloaded
}

public sealed record LinkStatusChange(DocumentKey Link, LinkStatus Status);

public readonly record struct GroupCount(long GroupId, string Name, int Count);

public sealed record ParameterInfo(ParameterKey Key, string DisplayName, ValueKind Kind);
