namespace DevTools.Mcp.Core;

/// <summary>Read/write index of capabilities advertised by connected host sessions.</summary>
public interface IConnectedHostCatalog
{
    void Replace(HostCatalogEntry entry);
    bool Remove(HostKey key);
    void Clear();
    IReadOnlyList<HostCatalogEntry> List();
    IReadOnlyList<HostCatalogHit> Search(
        string? query,
        IReadOnlyCollection<HostCatalogKind>? kinds = null,
        string? machineId = null,
        int? hostInstanceId = null,
        int limit = 50);
    HostCatalogResolution Resolve(HostCatalogKind kind, string target, string? machineId, int? hostInstanceId);
}
