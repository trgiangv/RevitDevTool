using System.Text.RegularExpressions;
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
                if (McpPipePattern.IsMatch(name))
                    pipes.Add(name);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.ZLogWarning(ex, $"MCP pipe scan error");
        }

        return pipes;
    }
}
