using System.IO.Compression;
using System.Text;                                          

namespace DevTools.McpServer.RevitFileInfo;

/// <summary>
/// Extracts the View Browser Organization tree from <c>Global/Latest</c>.
/// Paths like "03_Coordination>>MEP>>Pipes" represent how views are grouped
/// in the Revit Project Browser.
/// </summary>
internal static class BrowserOrganizationReader
{
    private const int StreamHeaderSize = 8;
    private const int GzipHeaderSize = 10;

    public static IReadOnlyList<string>? Read(RevitCompoundFile file)
    {
        using var ms = file.TryReadStream("Global", "Latest");
        if (ms is null) return null;

        var raw = ms.ToArray();
        if (raw.Length < StreamHeaderSize + GzipHeaderSize + 2)
            return null;

        byte[] decompressed;
        try
        {
            var deflateStart = StreamHeaderSize + GzipHeaderSize;
            using var input = new MemoryStream(raw, deflateStart, raw.Length - deflateStart, writable: false);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            deflate.CopyTo(output);
            decompressed = output.ToArray();
        }
        catch
        {
            return null;
        }

        if (decompressed.Length == 0) return null;

        var paths = ExtractBrowserPaths(decompressed);
        return paths.Count > 0 ? paths : null;
    }

    private static List<string> ExtractBrowserPaths(byte[] data)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();

        for (var i = 0; i < data.Length - 8; i += 2)
        {
            var len = ScanPrintableRun(data, i);
            if (len < 4) continue;

            var s = Encoding.Unicode.GetString(data, i, len * 2);
            i += len * 2 - 2;

            if (s.Contains(">>") && seen.Add(s))
                result.Add(s);
        }

        result.Sort(StringComparer.Ordinal);
        return result;
    }

    private static int ScanPrintableRun(byte[] data, int start)
    {
        var count = 0;
        for (var j = start; j < data.Length - 1; j += 2)
        {
            var lo = data[j];
            var hi = data[j + 1];
            if (hi == 0 && lo is >= 0x20 and < 0x7F)
                count++;
            else
                break;
        }
        return count;
    }
}
