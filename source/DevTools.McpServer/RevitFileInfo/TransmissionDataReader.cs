using System.Text;
using System.Xml.Serialization;
using JetBrains.Annotations;

namespace DevTools.McpServer.RevitFileInfo;

internal static class TransmissionDataReader
{
    private const string StreamName = "TransmissionData";
    private static readonly XmlSerializer Serializer = new(typeof(TransmissionData));

    public static TransmissionData? Read(RevitCompoundFile file)
    {
        using var ms = file.TryReadStream(StreamName);
        if (ms is null) return null;

        using var reader = new BinaryReader(ms, Encoding.Unicode);
        var length = reader.ReadInt32();
        var xml = new string(reader.ReadChars(length));

        using var textReader = new StringReader(xml);
        return Serializer.Deserialize(textReader) as TransmissionData;
    }
}

[XmlRoot("TransmissionData")]
public sealed record TransmissionData
{
    [XmlAttribute("isTransmitted")]
    public bool IsTransmitted { get; set; }

    [XmlAttribute("userData")]
    public string? UserData { get; set; }

    [XmlAttribute("version")]
    public int Version { get; set; }

    [XmlElement("ExternalFileReference")]
    public List<ExternalFileReference> ExternalFileReferences { get; set; } = [];
}

[PublicAPI]
public sealed record ExternalFileReference
{
    public int ElementId { get; set; }
    public string? ExternalFileReferenceType { get; set; }
    public string? LastSavedPath { get; set; }
    public string? LastSavedAbsolutePath { get; set; }
    public string? LastSavedCentralServerLocation { get; set; }
    public string? LastSavedPathType { get; set; }
    public string? LastSavedLoadState { get; set; }
    public string? DesiredPath { get; set; }
    public string? DesiredCentralServerLocation { get; set; }
    public string? DesiredPathType { get; set; }
    public string? DesiredLoadState { get; set; }
}
