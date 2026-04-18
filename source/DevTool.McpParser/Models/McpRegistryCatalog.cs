namespace DevTool.McpParser.Models;

public sealed record McpRegistryCatalog
{
    public IReadOnlyList<McpRegisteredTool> Tools { get; init; } = [];
    public IReadOnlyList<McpRegisteredPrompt> Prompts { get; init; } = [];
    public IReadOnlyList<McpRegisteredResource> Resources { get; init; } = [];

    public static McpRegistryCatalog Empty { get; } = new();

    public McpRegistryCatalog Merge(McpRegistryCatalog other)
    {
        return new McpRegistryCatalog
        {
            Tools = [.. Tools, .. other.Tools],
            Prompts = [.. Prompts, .. other.Prompts],
            Resources = [.. Resources, .. other.Resources],
        };
    }
}