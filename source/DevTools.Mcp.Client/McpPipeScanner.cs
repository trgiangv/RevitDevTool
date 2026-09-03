using System.Diagnostics;
using System.Text.RegularExpressions;
using DevTools.Ipc;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Mcp.Client;

internal sealed class McpPipeScanner(ILogger<McpPipeScanner> logger) : IMcpPipeScanner
{
    private static readonly Regex McpPipePattern = new(
        @"^DevToolsMcp_\w+_[^_]+_\d+$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IReadOnlyCollection<string> Discover()
    {
        var pipes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var path in Directory.GetFiles(@"\\.\pipe\"))
            {
                var name = Path.GetFileName(path);
                if (IsLiveMcpPipe(name))
                    pipes.Add(name);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.ZLogWarning(ex, $"MCP pipe scan error");
        }

        return pipes;
    }

    internal static bool IsLiveMcpPipe(string pipeName)
    {
        if (!McpPipePattern.IsMatch(pipeName))
            return false;
        if (!HostPipeName.TryParse(pipeName, out _, out _, out var pid))
            return false;
        return IsProcessAlive(pid);
    }

    private static bool IsProcessAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
