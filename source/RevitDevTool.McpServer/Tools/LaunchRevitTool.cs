using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.Win32;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace RevitDevTool.McpServer.Tools;

[SupportedOSPlatform("windows")]
public sealed class LaunchRevitTool : McpServerTool
{
    public override Tool ProtocolTool { get; } = new()
    {
        Name = "launch_revit",
        Description = "Launch Autodesk Revit by version number. Returns the process ID of the launched instance.",
        InputSchema = JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                versionNumber = new { type = "string", description = "Revit version year to launch (e.g., '2025', '2024')" }
            },
            required = new[] { "versionNumber" }
        })
    };

    public override IReadOnlyList<object> Metadata => [];

    public override ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        string? version = null;
        if (request.Params?.Arguments?.TryGetValue("versionNumber", out var versionElement) == true)
            version = versionElement.GetString();

        if (string.IsNullOrWhiteSpace(version))
            return ValueTask.FromResult(ToolHelpers.ErrorResult("versionNumber is required."));

        var revitPath = FindRevitPath(version);
        if (revitPath is null)
            return ValueTask.FromResult(ToolHelpers.ErrorResult($"Revit {version} installation not found."));

        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = revitPath,
                UseShellExecute = true
            });

            if (process is null)
                return ValueTask.FromResult(ToolHelpers.ErrorResult("Failed to start Revit process."));

            var result = new { processId = process.Id, version, path = revitPath };
            return ValueTask.FromResult(new CallToolResult
            {
                Content = [new TextContentBlock { Text = JsonSerializer.Serialize(result) }]
            });
        }
        catch (Exception ex)
        {
            return ValueTask.FromResult(ToolHelpers.ErrorResult($"Failed to launch Revit: {ex.Message}"));
        }
    }

    private static string? FindRevitPath(string version)
    {
        var registryPath = FindFromRegistry(version);
        if (registryPath is not null) return registryPath;

        var defaultPath = $@"C:\Program Files\Autodesk\Revit {version}\Revit.exe";
        return File.Exists(defaultPath) ? defaultPath : null;
    }

    private static string? FindFromRegistry(string version)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                $@"SOFTWARE\Autodesk\Revit\Autodesk Revit {version}");
            if (key?.GetValue("InstallationLocation") is string installDir)
            {
                var exePath = Path.Combine(installDir, "Revit.exe");
                if (File.Exists(exePath)) return exePath;
            }
        }
        catch
        {
            // Registry access may fail
        }

        return null;
    }
}
