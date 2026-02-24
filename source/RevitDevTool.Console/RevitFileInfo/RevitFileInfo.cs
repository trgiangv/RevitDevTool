using System.Text;
using OpenMcdf;
using RevitDevTool.Console.RevitFileInfo.BasicFileStream;
using RevitDevTool.Console.RevitFileInfo.TransmissionDataStream;

namespace RevitDevTool.Console.RevitFileInfo;

public class RevitFileInfo
{
    public static readonly IReadOnlyList<string> RevitFilesExtensions = [".rvt", ".rfa", ".rte"];

    public RevitFileInfo(string modelPath)
    {
        if (string.IsNullOrEmpty(modelPath))
            throw new ArgumentException("Value cannot be null or empty.", nameof(modelPath));

        if (!File.Exists(modelPath))
            throw new ArgumentException("Revit document was not found.", nameof(modelPath));

        if (!RevitFilesExtensions.Contains(Path.GetExtension(modelPath), StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"Invalid extension, allowed: \"{string.Join(", ", RevitFilesExtensions)}\".", nameof(modelPath));

        ModelPath = modelPath;

        using var root = RootStorage.OpenRead(modelPath);
        BasicFileInfo = ReadBasicFileInfo(root);
        TransmissionData = ReadTransmissionData(root);
    }

    public string ModelPath { get; }
    public BasicFileInfo? BasicFileInfo { get; }
    public TransmissionData? TransmissionData { get; }

    public int? GetRevitYear()
    {
        var format = BasicFileInfo?.AppInfo.Format;
        if (string.IsNullOrEmpty(format)) return null;
        return int.TryParse(format, out var year) ? year : null;
    }

    private static BasicFileInfo? ReadBasicFileInfo(RootStorage root)
    {
        try
        {
            using var cfbStream = root.OpenStream(BasicFileInfo.BasicFileInfoName);
            using var ms = new MemoryStream();
            cfbStream.CopyTo(ms);
            ms.Position = 0;
            using var reader = new BinaryReader(ms, Encoding.Unicode);
            return BasicFileInfo.ReadFromReader(reader);
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static TransmissionData? ReadTransmissionData(RootStorage root)
    {
        try
        {
            using var cfbStream = root.OpenStream(TransmissionData.TransmissionDataFileName);
            using var ms = new MemoryStream();
            cfbStream.CopyTo(ms);
            return TransmissionData.GetXmlTransmissionData(ms.ToArray());
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }
}
