using System.Xml.Serialization;

namespace RevitDevTool.Console.RevitFileInfo.TransmissionDataStream;

public enum PathType
{
    Absolute,
    Relative,
    [XmlEnum(Name = "Server Location")] ServerLocation,
    [XmlEnum(Name = "Relative to Central Model")] RelativeCentralModel,
    [XmlEnum(Name = "Relative to Library Locations")] RelativeLibraryLocations
}
