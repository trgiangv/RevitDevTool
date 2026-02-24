using System.Text;
using RFI = RevitDevTool.Console.RevitFileInfo;
// ReSharper disable ConvertToExtensionBlock

namespace RevitDevTool.Console.RevitFileInfo.BasicFileStream;

internal static class BinaryReaderExtensions
{
    public static ModelVersionInfo ReadCentralVersion(this BinaryReader reader)
    {
        var versionNumber = reader.ReadInt32();
        var id = reader.ReadGuid();
        return new ModelVersionInfo(id, versionNumber);
    }

    public static ModelVersionInfo ReadCurrentVersion(this BinaryReader reader)
    {
        var id = reader.ReadGuid();
        var versionNumber = Convert.ToInt32(reader.ReadValueString());
        return new ModelVersionInfo(id, versionNumber);
    }

    public static LanguageCode ReadLanguageCode(this BinaryReader reader)
    {
        return RFI.LanguageCode.GetLanguageCode(reader.ReadValueString() ?? "");
    }

    public static WorksharingType ReadWorksharingType(this BinaryReader reader)
    {
        return (WorksharingType)(reader.ReadByte() + 1);
    }

    public static ModelIdentity ReadIdentity(this BinaryReader reader)
    {
        return new ModelIdentity(reader.ReadGuid());
    }

    public static Guid ReadGuid(this BinaryReader reader)
    {
        return new Guid(reader.ReadValueString() ?? "");
    }

    public static string? ReadValueString(this BinaryReader reader)
    {
        var length = reader.ReadInt32();
        return length <= 0 ? null : new string(reader.ReadChars(length));
    }

    public static StringBuilder AppendLineFormat(this StringBuilder builder, string propertyName, object? value)
    {
        return builder.AppendLine().Append(propertyName).Append(": ").Append(value);
    }
}
