using System.Xml.Serialization;

namespace RevitDevTool.Console.RevitFileInfo.TransmissionDataStream;

public enum LoadState
{
    Loaded,
    Unloaded,
    [XmlEnum(Name = "Not Found")] NotFound
}
