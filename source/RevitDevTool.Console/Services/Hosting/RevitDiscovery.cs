using System.Diagnostics;
using RevitDevTool.Bridge.Abstractions;
using RevitDevTool.Bridge.IPC;

namespace RevitDevTool.Console.Services.Hosting;

/// <summary>
/// Discovers running Revit instances by scanning <c>\\.\pipe\</c> for RevitDevTool pipes.
/// </summary>
public sealed class RevitDiscovery : IHostDiscovery
{
    public string AppId => "revit";

    public List<IHostInstance> Discover()
    {
        var instances = new List<IHostInstance>();

        foreach (var pipeName in EnumerateRevitDevToolPipes())
        {
            if (PipeNaming.TryParse(pipeName, out var appId, out var version, out var pid)
                && appId == AppId
                && IsProcessAlive(pid))
            {
                instances.Add(new RevitHostInstance(version, pid, pipeName));
            }
        }

        return instances;
    }

    private static IEnumerable<string> EnumerateRevitDevToolPipes()
    {
        try
        {
            return Directory.GetFiles(@"\\.\pipe\", $"{PipeNaming.Prefix}_*")
                .Select(Path.GetFileName)
                .Where(n => n != null)
                .Cast<string>();
        }
        catch
        {
            return [];
        }
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
