using System.Security.Cryptography;
using System.Text;

namespace DevTools.Daemon.Mcp.Processes;

/// <summary>Daemon-local locator. Identity is (processId, kind, target); the hash is freshness only.</summary>
public sealed record CatalogId(int ProcessId, CatalogType Kind, string Target, string ContentHash)
{
    private const string Prefix = "dci2.";

    public string Encode() =>
        $"{Prefix}{ProcessId}.{CatalogTypeCodec.Code(Kind)}.{ContentHash}.{Target}";

    public static bool TryDecode(string? value, out CatalogId? id)
    {
        id = null;
        if (value is null || !value.StartsWith(Prefix, StringComparison.Ordinal))
            return false;

        var body = value[Prefix.Length..];
        var parts = body.Split('.');
        if (parts.Length < 4)
            return false;

        if (!int.TryParse(parts[0], out var processId) || processId <= 0)
            return false;

        if (parts[1].Length != 1 || !CatalogTypeCodec.TryFromCode(parts[1][0], out var kind))
            return false;

        var contentHash = parts[2];
        if (contentHash.Length != 8)
            return false;

        var target = string.Join('.', parts.AsSpan(3));
        if (string.IsNullOrWhiteSpace(target))
            return false;

        id = new CatalogId(processId, kind, target, contentHash);
        return true;
    }

    public static string ContentHashFor(CatalogItem item)
    {
        var name = item.Tool?.Name ?? item.Resource?.Name ?? item.ResourceTemplate?.Name ?? item.Target;
        var schema = item.Tool?.InputSchema.GetRawText() ?? string.Empty;
        return ComputeContentHash(name, item.Description, schema);
    }

    public static string ComputeContentHash(string name, string? description, string schema)
    {
        var source = string.Join("\n", name, description ?? string.Empty, schema);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return Convert.ToHexString(hash)[..8].ToLowerInvariant();
    }
}
