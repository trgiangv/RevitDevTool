using System.IO.Compression;
using System.Text;

namespace RevitDevTool.Server.RevitFileInfo;

internal static class WorksetParser
{
    private static readonly byte[] GzipMagic = [0x1F, 0x8B];

    public static IReadOnlyList<WorksetInfoDto> TryParse(string filePath)
    {
        byte[] rawPartitionTable;
        try
        {
            rawPartitionTable = ReadOleStream(filePath, "PartitionTable");
        }
        catch
        {
            return [];
        }

        var decompressed = DecompressPartitionTable(rawPartitionTable);
        if (decompressed.Length == 0)
            return [];

        return ParseUserWorksets(decompressed);
    }

    private static byte[] ReadOleStream(string filePath, string streamName)
    {
        var raw = File.ReadAllBytes(filePath);
        var sectorSize = 1 << ReadUInt16(raw, 30);

        var difat = new List<uint>();
        for (var i = 0; i < 109; i++)
        {
            var sec = ReadUInt32(raw, 76 + i * 4);
            if (sec < 0xFFFFFFFE)
                difat.Add(sec);
        }

        var fat = new List<uint>();
        foreach (var sec in difat)
        {
            for (var i = 0; i < sectorSize / 4; i++)
                fat.Add(ReadUInt32(raw, (int)((sec + 1) * (uint)sectorSize) + i * 4));
        }

        byte[] Chain(uint start, uint? size = null)
        {
            var output = new List<byte>();
            var seen = new HashSet<uint>();
            var sec = start;

            while (sec < 0xFFFFFFFE)
            {
                if (seen.Contains(sec) || sec >= fat.Count)
                    break;

                seen.Add(sec);
                var begin = (int)((sec + 1) * (uint)sectorSize);
                var end = begin + sectorSize;
                if (begin < 0 || end > raw.Length) break;

                for (var j = begin; j < end; j++)
                    output.Add(raw[j]);

                sec = fat[(int)sec];
            }

            var bytes = output.ToArray();
            if (size is null || size.Value >= bytes.Length)
                return bytes;

            var trimmed = new byte[size.Value];
            Buffer.BlockCopy(bytes, 0, trimmed, 0, (int)size.Value);
            return trimmed;
        }

        var rootStart = ReadUInt32(raw, 48);
        var directoryData = Chain(rootStart);

        for (var i = 0; i < directoryData.Length / 128; i++)
        {
            var entryOffset = i * 128;
            var nameLength = ReadUInt16(directoryData, entryOffset + 64);
            var name = string.Empty;
            if (nameLength > 0)
            {
                var charBytes = Math.Max(0, nameLength - 2);
                name = Encoding.Unicode.GetString(directoryData, entryOffset, charBytes);
            }

            if (!string.Equals(name, streamName, StringComparison.Ordinal))
                continue;

            var streamStart = ReadUInt32(directoryData, entryOffset + 116);
            var streamSize = ReadUInt32(directoryData, entryOffset + 120);
            return Chain(streamStart, streamSize);
        }

        throw new InvalidOperationException($"Stream '{streamName}' not found.");
    }

    private static byte[] DecompressPartitionTable(byte[] raw)
    {
        if (raw.Length < 2)
            return [];

        for (var i = 0; i < raw.Length - 1; i++)
        {
            if (raw[i] != GzipMagic[0] || raw[i + 1] != GzipMagic[1])
                continue;

            try
            {
                using var input = new MemoryStream(raw, i, raw.Length - i, writable: false);
                using var gzip = new GZipStream(input, CompressionMode.Decompress, leaveOpen: true);
                using var output = new MemoryStream();
                gzip.CopyTo(output);
                if (output.Length > 0)
                    return output.ToArray();
            }
            catch
            {
                // Not a valid gzip member at this offset.
            }
        }

        return [];
    }

    private static IReadOnlyList<WorksetInfoDto> ParseUserWorksets(byte[] dec)
    {
        var result = new List<WorksetInfoDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var i = 0;
        var n = dec.Length;
        while (i < n - 16)
        {
            if (!HasWorksetRecordPrefix(dec, i))
            {
                i++;
                continue;
            }

            try
            {
                var charCount = BitConverter.ToInt32(dec, i + 9);
                if (charCount is < 1 or > 200)
                {
                    i++;
                    continue;
                }

                var stringStart = i + 13;
                var stringEnd = stringStart + charCount * 2;
                if (stringEnd + 4 > n)
                {
                    i++;
                    continue;
                }

                var name = System.Text.Encoding.Unicode.GetString(dec, stringStart, charCount * 2);
                if (!IsAsciiPrintable(name))
                {
                    i++;
                    continue;
                }

                if (!IsToolRecordMarker(dec, stringEnd))
                {
                    i++;
                    continue;
                }

                var afterMarker = stringEnd + 4;
                int? worksetId;
                string? guid;
                (worksetId, guid) = TryReadGuidAndWorksetId(dec, afterMarker);

                var dedupKey = $"{worksetId}:{name}";
                if (seen.Add(dedupKey))
                {
                    result.Add(new WorksetInfoDto
                    {
                        WorksetId = worksetId,
                        Name = name,
                        Guid = guid
                    });
                }

                i = afterMarker + (guid is null ? 4 : 20);
                continue;
            }
            catch
            {
                // Ignore malformed candidate and continue scanning.
            }

            i++;
        }

        return result;
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
                return (idWithGuid, Convert.ToHexString(guidCandidate).ToLowerInvariant());
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

    private static ushort ReadUInt16(byte[] buffer, int offset)
    {
        return (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
    }

    private static uint ReadUInt32(byte[] buffer, int offset)
    {
        return (uint)(
            buffer[offset]
            | (buffer[offset + 1] << 8)
            | (buffer[offset + 2] << 16)
            | (buffer[offset + 3] << 24));
    }
}

internal sealed class WorksetInfoDto
{
    public int? WorksetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Guid { get; set; }
}
