using DevTools.Update.Apply;
using DevTools.Update.Autodesk;

namespace DevTools.Update.Engine;

/// <summary>
///     The payloads of one version that one apply touched.
/// </summary>
internal sealed class PayloadBatch(string version)
{
    public string Version { get; } = version;
    public List<AddinTarget> Applied { get; } = [];
    public List<(AddinTarget Target, IReadOnlyList<LockingProcess> Lockers)> Pending { get; } = [];
    public List<AddinTarget> Unavailable { get; } = [];
    public bool Changed => Applied.Count > 0 || Pending.Count > 0;
    public AddinTarget[] PendingTargets => Pending.Select(item => item.Target).ToArray();
}
