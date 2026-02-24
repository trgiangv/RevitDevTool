namespace RevitDevTool.Console.RevitFileInfo.BasicFileStream;

public class ModelVersionInfo
{
    public static readonly ModelVersionInfo Empty = new(default, default);

    internal ModelVersionInfo(Guid id, int versionNumber)
    {
        Id = id;
        VersionNumber = versionNumber;
    }

    public Guid Id { get; }
    public int VersionNumber { get; }

    public override string ToString() => $"{VersionNumber} - {Id}";
}
