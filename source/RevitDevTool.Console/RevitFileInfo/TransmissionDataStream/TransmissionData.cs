using System.Text;
using System.Xml.Serialization;

namespace RevitDevTool.Console.RevitFileInfo.TransmissionDataStream;

public class TransmissionData
{
    public const string TransmissionDataFileName = "TransmissionData";

    [XmlAttribute("isTransmitted")]
    public bool IsTransmitted { get; set; }

    [XmlAttribute("userData")]
    public string? UserData { get; set; }

    [XmlAttribute("version")]
    public int Version { get; set; }

    [XmlElement("ExternalFileReference")]
    public List<ExternalFileReference> ExternalFileReferences { get; set; } = [];

    internal static TransmissionData GetXmlTransmissionData(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = new BinaryReader(stream, Encoding.Unicode);
        var length = reader.ReadInt32();
        var xmlData = new string(reader.ReadChars(length));

        using var textReader = new StringReader(xmlData);
        var xmlSerializer = new XmlSerializer(typeof(TransmissionData));
        return (TransmissionData?)xmlSerializer.Deserialize(textReader) ?? new TransmissionData();
    }

    public override string ToString() =>
        $"IsTransmitted: {IsTransmitted}; Count: {ExternalFileReferences.Count}";
}
