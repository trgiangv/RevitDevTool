using DevTools.Update.Apply;

namespace DevTools.Update.Engine;

/// <summary>
///     The payloads of one version that one apply touched.
/// </summary>
internal sealed class PayloadBatch(string version)
{
    public string Version { get; } = version;
    public List<string> Applied { get; } = [];
    public List<(string HostVersion, IReadOnlyList<LockingProcess> Lockers)> Pending { get; } = [];
    public List<string> Unavailable { get; } = [];
    public bool Changed => Applied.Count > 0 || Pending.Count > 0;
    public string[] PendingHostVersions => Pending.Select(item => item.HostVersion).ToArray();
}
