namespace RevitDevTool.Bridge.Abstractions;

/// <summary>
/// Launches new host application instances and waits for their engine pipes to become available.
/// </summary>
public interface IHostLauncher
{
    string AppId { get; }

    Task<IHostInstance> LaunchAsync(string version, TimeSpan timeout, CancellationToken ct = default);

    Task<List<IHostInstance>> EnsureInstancesAsync(
        IEnumerable<string> requiredVersions,
        IReadOnlyList<IHostInstance> existingInstances,
        TimeSpan timeout,
        CancellationToken ct = default);
}
