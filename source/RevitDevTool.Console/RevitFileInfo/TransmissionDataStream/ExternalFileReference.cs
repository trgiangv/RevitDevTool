namespace RevitDevTool.Console.RevitFileInfo.TransmissionDataStream;

public class ExternalFileReference
{
    public int ElementId { get; set; }
    public ExternalFileReferenceType ExternalFileReferenceType { get; set; }
    public string? LastSavedPath { get; set; }
    public string? LastSavedAbsolutePath { get; set; }
    public string? LastSavedCentralServerLocation { get; set; }
    public PathType LastSavedPathType { get; set; }
    public LoadState LastSavedLoadState { get; set; }
    public string? DesiredPath { get; set; }
    public string? DesiredCentralServerLocation { get; set; }
    public PathType DesiredPathType { get; set; }
    public LoadState DesiredLoadState { get; set; }

    public override string ToString() => $"ElementId: {ElementId}; RefType: {ExternalFileReferenceType}";
}
