namespace RevitDevTool.Bridge.Abstractions;

/// <summary>
/// Discovers running host application instances that have an active engine pipe.
/// </summary>
public interface IHostDiscovery
{
    string AppId { get; }
    List<IHostInstance> Discover();
}
