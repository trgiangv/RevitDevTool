namespace DevTools.ModelsContext.Model;

public enum HostFilterKind
{
    RuleBased,
    Selection
}

public sealed record HostFilterInfo(DocumentKey Document, long Id, string Name, HostFilterKind Kind, bool InView);

public sealed record CustomFilter(string Name, FilterDefinition Definition);
