using System.Collections.Concurrent;
using DevTools.Mcp.Core.Sessions;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.Mcp.Client;

/// <summary>In-memory capability index for host sessions owned by <see cref="HostBroker"/>.</summary>
public sealed class ConnectedHostCatalog : IConnectedHostCatalog
{
    private static readonly HostCatalogKind[] AllKinds = Enum.GetValues<HostCatalogKind>();
    private readonly ConcurrentDictionary<HostKey, HostCatalogEntry> _entries = new();

    public void Replace(HostCatalogEntry entry) => _entries[entry.Key] = entry;

    public bool Remove(HostKey key) => _entries.TryRemove(key, out _);

    public void Clear() => _entries.Clear();

    public IReadOnlyList<HostCatalogEntry> List() =>
        _entries.Values.OrderBy(entry => entry.Key.ProcessId).ToArray();

    public IReadOnlyList<HostCatalogHit> Search(
        string? query,
        IReadOnlyCollection<HostCatalogKind>? kinds = null,
        string? machineId = null,
        int? hostInstanceId = null,
        int limit = 50)
    {
        limit = Math.Clamp(limit <= 0 ? 50 : limit, 1, 500);
        var needle = string.IsNullOrWhiteSpace(query) ? null : query!.Trim();

        if (needle is null)
        {
            return EnumerateHits(machineId, hostInstanceId, kinds)
                .OrderBy(hit => hit.Kind)
                .ThenBy(hit => hit.Target, StringComparer.OrdinalIgnoreCase)
                .ThenBy(hit => hit.Key.ProcessId)
                .Take(limit)
                .ToArray();
        }

        return EnumerateHits(machineId, hostInstanceId, kinds)
            .Select(hit => (Hit: hit, Rank: RankMatch(needle, hit)))
            .Where(match => match.Rank > 0)
            .OrderBy(match => match.Rank)
            .ThenBy(match => match.Hit.Kind)
            .ThenBy(match => match.Hit.Target, StringComparer.OrdinalIgnoreCase)
            .ThenBy(match => match.Hit.Key.ProcessId)
            .Take(limit)
            .Select(match => match.Hit)
            .ToArray();
    }

    public HostCatalogResolution Resolve(
        HostCatalogKind kind,
        string target,
        string? machineId,
        int? hostInstanceId)
    {
        if (string.IsNullOrWhiteSpace(target))
            return new HostCatalogResolution(HostCatalogResolutionState.NotFound, null, []);

        var candidates = EnumerateHits(machineId, hostInstanceId, [kind])
            .Where(hit => string.Equals(hit.Target, target, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return candidates.Length switch
        {
            0 => new HostCatalogResolution(HostCatalogResolutionState.NotFound, null, candidates),
            1 => new HostCatalogResolution(HostCatalogResolutionState.Found, candidates[0], candidates),
            _ => new HostCatalogResolution(HostCatalogResolutionState.Ambiguous, null, candidates)
        };
    }

    private static int RankMatch(string needle, HostCatalogHit hit)
    {
        var normalizedNeedle = Normalize(needle);
        var tokens = normalizedNeedle.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var target = Normalize(hit.Target);
        var name = Normalize(hit.Resource?.Name ?? hit.ResourceTemplate?.Name ?? string.Empty);
        var description = Normalize(hit.Description ?? string.Empty);
        if (target == normalizedNeedle) return 1;
        if (target.StartsWith(normalizedNeedle, StringComparison.Ordinal)) return 2;
        if (tokens.All(token => target.Contains(token, StringComparison.Ordinal))) return 3;
        if (tokens.All(token => name.Contains(token, StringComparison.Ordinal))) return 4;
        if (tokens.All(token => description.Contains(token, StringComparison.Ordinal))) return 5;
        return target.Contains(normalizedNeedle, StringComparison.Ordinal) || name.Contains(normalizedNeedle, StringComparison.Ordinal) || description.Contains(normalizedNeedle, StringComparison.Ordinal) ? 6 : 0;
    }

    private static string Normalize(string value) => string.Join(" ", value
        .Replace('_', ' ')
        .Replace('-', ' ')
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        .Select(token => token.ToLowerInvariant()));

    private IEnumerable<HostCatalogHit> EnumerateHits(
        string? machineId,
        int? hostInstanceId,
        IReadOnlyCollection<HostCatalogKind>? kinds)
    {
        var kindFilter = kinds is { Count: > 0 } ? kinds : AllKinds;
        foreach (var entry in List())
        {
            if (!MatchesHost(entry.Key, machineId, hostInstanceId))
                continue;

            foreach (var kind in kindFilter)
            foreach (var hit in HitsOfKind(entry, kind))
                yield return hit;
        }
    }

    private static bool MatchesHost(HostKey key, string? machineId, int? hostInstanceId) =>
        (machineId is null || string.Equals(key.MachineId, machineId, StringComparison.OrdinalIgnoreCase)) &&
        (hostInstanceId is null || key.ProcessId == hostInstanceId);

    private static IEnumerable<HostCatalogHit> HitsOfKind(HostCatalogEntry entry, HostCatalogKind kind) => kind switch
    {
        HostCatalogKind.Tool => entry.Tools.Select(tool => new HostCatalogHit(HostCatalogKind.Tool, tool.Name, tool.Description, entry.Key, entry.Instance, Tool: tool)),
        HostCatalogKind.Resource => entry.Resources.Select(resource => new HostCatalogHit(HostCatalogKind.Resource, resource.Uri, resource.Description, entry.Key, entry.Instance, Resource: resource)),
        HostCatalogKind.ResourceTemplate => entry.ResourceTemplates.Select(template => new HostCatalogHit(HostCatalogKind.ResourceTemplate, template.UriTemplate, template.Description, entry.Key, entry.Instance, ResourceTemplate: template)),
        _ => []
    };
}
