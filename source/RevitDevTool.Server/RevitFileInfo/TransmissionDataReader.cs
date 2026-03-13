using System.Text;
using System.Xml.Serialization;
using OpenMcdf;

namespace RevitDevTool.Server.RevitFileInfo;

internal static class TransmissionDataReader
{
    private const string StreamName = "TransmissionData";

    public static TransmissionDataDto? Read(string filePath)
    {
        using var storage = RootStorage.OpenRead(filePath);
        CfbStream stream;
        try
        {
            stream = storage.OpenStream(StreamName);
        }
        catch
        {
            return null;
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;

        using var reader = new BinaryReader(ms, Encoding.Unicode);
        var length = reader.ReadInt32();
        var xml = new string(reader.ReadChars(length));

        var serializer = new XmlSerializer(typeof(TransmissionDataDto));
        using var textReader = new StringReader(xml);
        return serializer.Deserialize(textReader) as TransmissionDataDto;
    }
}

[XmlRoot("TransmissionData")]
public sealed class TransmissionDataDto
{
    [XmlAttribute("isTransmitted")]
    public bool IsTransmitted { get; set; }

    [XmlAttribute("userData")]
    public string? UserData { get; set; }

    [XmlAttribute("version")]
    public int Version { get; set; }

    [XmlElement("ExternalFileReference")]
    public List<ExternalFileReferenceDto> ExternalFileReferences { get; set; } = [];
}

public sealed class ExternalFileReferenceDto
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
