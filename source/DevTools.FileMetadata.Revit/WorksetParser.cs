namespace DevTools.FileMetadata.Revit;

internal static class WorksetParser
{
    public static IReadOnlyList<WorksetInfo> TryParse(byte[] decompressedPartitionTable)
    {
        return decompressedPartitionTable.Length == 0
            ? []
            : ParseUserWorksets(decompressedPartitionTable);
    }

    private static List<WorksetInfo> ParseUserWorksets(byte[] dec)
    {
        var result = new List<WorksetInfo>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < dec.Length - 16; i++)
        {
            if (!HasWorksetRecordPrefix(dec, i))
                continue;

            var advance = TryReadRecord(dec, i, seen, result);
            if (advance > 0)
                i += advance - 1;
        }

        return result;
    }

    /// <summary>
    /// Attempts to read a workset record from the given byte array at the specified offset.
    /// </summary>
    /// <returns>Number of bytes to skip past this record, or 0 if not a valid record.</returns>
    private static int TryReadRecord(byte[] dec, int offset, HashSet<string> seen, List<WorksetInfo> result)
    {
        try
        {
            var charCount = BitConverter.ToInt32(dec, offset + 9);
            if (charCount is < 1 or > 200)
                return 0;

            var stringStart = offset + 13;
            var stringEnd = stringStart + charCount * 2;
            if (stringEnd + 4 > dec.Length)
                return 0;

            var name = System.Text.Encoding.Unicode.GetString(dec, stringStart, charCount * 2);
            if (!IsAsciiPrintable(name) || !IsToolRecordMarker(dec, stringEnd))
                return 0;

            var afterMarker = stringEnd + 4;
            var (worksetId, guid) = TryReadGuidAndWorksetId(dec, afterMarker);

            if (seen.Add($"{worksetId}:{name}"))
                result.Add(new WorksetInfo { WorksetId = worksetId, Name = name, Guid = guid });

            return afterMarker - offset + (guid is null ? 4 : 20);
        }
        catch
        {
            return 0;
        }
    }

    private static bool HasWorksetRecordPrefix(byte[] data, int offset)
    {
        return data[offset + 0] == 0xFF
               && data[offset + 1] == 0xFF
               && data[offset + 2] == 0xFF
               && data[offset + 3] == 0xFF
               && data[offset + 4] == 0x00
               && data[offset + 5] == 0x00
               && data[offset + 6] == 0x00
               && data[offset + 7] == 0x00
               && data[offset + 8] == 0x00;
    }

    private static bool IsToolRecordMarker(byte[] data, int offset)
    {
        return data[offset + 0] == 0x01
               && data[offset + 1] == 0x00
               && data[offset + 2] == 0x00
               && data[offset + 3] == 0x00;
    }

    private static (int? WorksetId, string? Guid) TryReadGuidAndWorksetId(byte[] data, int offset)
    {
        if (offset + 20 <= data.Length)
        {
            var guidCandidate = new byte[16];
            Buffer.BlockCopy(data, offset, guidCandidate, 0, 16);
            var idWithGuid = BitConverter.ToInt32(data, offset + 16);

            if (LooksLikeGuid(guidCandidate))
                return (idWithGuid, Convert.ToHexStringLower(guidCandidate));
        }

        if (offset + 4 <= data.Length)
            return (BitConverter.ToInt32(data, offset), null);

        return (null, null);
    }

    private static bool LooksLikeGuid(byte[] bytes)
    {
        var min = byte.MaxValue;
        var max = byte.MinValue;
        foreach (var b in bytes)
        {
            if (b < min) min = b;
            if (b > max) max = b;
        }

        return max - min > 50;
    }

    private static bool IsAsciiPrintable(string value)
    {
        foreach (var c in value)
        {
            if (c is < ' ' or > '~')
                return false;
        }

        return true;
    }
}

[PublicAPI]
public sealed record WorksetInfo
{
    public int? WorksetId { get; init; }
    public required string Name { get; init; }
    public string? Guid { get; init; }
}
