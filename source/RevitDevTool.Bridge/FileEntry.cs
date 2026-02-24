namespace RevitDevTool.Bridge;

/// <summary>
/// Per-file configuration entry.
/// Null fields fall back to <see cref="BatchConfig.Defaults"/>.
/// </summary>
public sealed class FileEntry : JobOptions
{
    public string Path { get; set; } = "";
}
