using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;
using DevTools.Mcp.Core;

namespace DevTools.Mcp.Server.Contracts;

/// <summary>Opaque, daemon-local locator for a catalog capability. It is not an identity or a secret.</summary>
public sealed record DynamicCapabilityId(
    string MachineId,
    int HostInstanceId,
    HostCatalogKind Kind,
    string Target,
    string CatalogVersion,
    string Fingerprint)
{
    private const string Prefix = "dci1.";
    private static readonly ConditionalWeakTable<HostCatalogEntry, CatalogVersionHolder> CatalogVersions = new();

    /// <summary>Version is local to the catalog entry object and changes on replacement.</summary>
    public static string CatalogVersionFor(HostCatalogEntry entry) => CatalogVersions.GetValue(entry, _ => new CatalogVersionHolder()).Value;

    public string Encode() => Prefix + Base64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(this));

    public static bool TryDecode(string? value, out DynamicCapabilityId? capabilityId)
    {
        capabilityId = null;
        if (value is null || string.IsNullOrWhiteSpace(value) || !value.StartsWith(Prefix, StringComparison.Ordinal))
            return false;

        try
        {
            capabilityId = JsonSerializer.Deserialize<DynamicCapabilityId>(Base64Url.Decode(value![Prefix.Length..]));
            if (capabilityId is null)
                return false;
            return !string.IsNullOrWhiteSpace(capabilityId.MachineId) && capabilityId.HostInstanceId > 0 &&
                   !string.IsNullOrWhiteSpace(capabilityId.Target) && !string.IsNullOrWhiteSpace(capabilityId.CatalogVersion) &&
                   !string.IsNullOrWhiteSpace(capabilityId.Fingerprint);
        }
        catch (JsonException) { return false; }
        catch (FormatException) { return false; }
    }

    public static string FingerprintFor(HostCatalogHit hit)
    {
        var schema = hit.Tool?.InputSchema.GetRawText() ?? string.Empty;
        var source = string.Join("\n", hit.Kind, hit.Target, hit.Description ?? string.Empty, hit.Resource?.MimeType ?? hit.ResourceTemplate?.MimeType ?? string.Empty, schema);
        using var sha = System.Security.Cryptography.SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(source))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private sealed class CatalogVersionHolder
    {
        public string Value { get; } = Guid.NewGuid().ToString("N");
    }

    private static class Base64Url
    {
        public static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        public static byte[] Decode(string value)
        {
            var base64 = value.Replace('-', '+').Replace('_', '/');
            return Convert.FromBase64String(base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='));
        }
    }
}
