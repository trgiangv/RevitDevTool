using System.Diagnostics;
using DevTools.Daemon.Contracts;
using DevTools.Daemon.Mcp.AcadFileInfo;
using DevTools.Daemon.Mcp.Tools.Utils;
using DevTools.Logging;

namespace DevTools.Daemon.Hosts;

public sealed class AcadHostDriver : IHostDriver
{
    public string HostId => nameof(HostApp.AutoCad);
    public IReadOnlySet<HostApp> SupportedHostApps { get; } = new HashSet<HostApp>
    {
        HostApp.AutoCad, HostApp.Civil3D, HostApp.Plant3D,
        HostApp.AcadArch, HostApp.AcadMech, HostApp.AcadElec,
        HostApp.AcadMep, HostApp.AcadMap3D
    };
    public IReadOnlySet<string> FileExtensions { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".dwg", ".dxf", ".dwf", ".dwt" };

    public bool SupportsVersion(string version) => true;

    public Task<HostLaunchResult> LaunchAsync(HostLaunchRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.FilePath) && !File.Exists(request.FilePath))
            throw new HostDriverException($"File not found: {request.FilePath}");

        var version = string.IsNullOrWhiteSpace(request.VersionNumber)
            ? AcadPathResolver.GetInstalledVersions().FirstOrDefault()
            : request.VersionNumber;
        if (version is null)
            throw new HostDriverException("No AutoCAD-family installation found.");

        var exePath = AcadPathResolver.FindAcadPath(version, request.RequestedHostApp);
        if (string.IsNullOrWhiteSpace(exePath))
            throw new HostDriverException($"{request.RequestedHostApp} {version} installation not found.");

        var arguments = new List<string> { "/nologo" };
        if (!string.IsNullOrWhiteSpace(request.FilePath))
            arguments.Add(request.FilePath);

        var process = StartProcess(request.RequestedHostApp, exePath, arguments);
        return Task.FromResult(new HostLaunchResult(
            request.RequestedHostApp,
            process.Id,
            version,
            exePath,
            null,
            arguments,
            HostLaunchCoordinator.StartDialogResolver(process.Id, ct)));
    }

    public Task<FileInfoResult> ReadFileInfoAsync(string filePath, CancellationToken ct)
    {
        var info = DwgFileInfoReader.Read(filePath);
        return Task.FromResult<FileInfoResult>(new DwgFileInfoResult
        {
            HostApp = HostApp.AutoCad,
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            AcadVersion = info.AcadVersion,
            Title = info.Title,
            Subject = info.Subject,
            Author = info.Author,
            Keywords = info.Keywords,
            Comments = info.Comments,
            LastSavedBy = info.LastSavedBy,
            LayerCount = info.LayerCount,
            BlockCount = info.BlockCount,
            Layers = info.Layers
        });
    }

    private static Process StartProcess(HostApp hostApp, string exePath, IReadOnlyList<string> arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory
            };
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            return Process.Start(startInfo)
                ?? throw new HostDriverException($"Failed to start {hostApp} process.");
        }
        catch (HostDriverException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new HostDriverException($"Failed to launch {hostApp}: {ex.Message}");
        }
    }
}
