using System.Diagnostics;
using System.Text.RegularExpressions;
using DevTools.Daemon.Contracts;
using DevTools.Daemon.Mcp.RevitFileInfo;
using DevTools.Daemon.Mcp.Tools.Utils;
using DevTools.Logging;

namespace DevTools.Daemon.Hosts;

internal sealed partial class RevitHostDriver : IHostDriver
{
    private static readonly HashSet<string> SupportedLanguageCodes =
    [
        "ENU", "ENG", "FRA", "DEU", "ITA", "JPN", "KOR",
        "PLK", "ESP", "CHS", "CHT", "PTB", "RUS", "CSY", "HUN"
    ];

    public string HostId => nameof(HostApp.Revit);
    public IReadOnlySet<HostApp> SupportedHostApps { get; } = new HashSet<HostApp> { HostApp.Revit };
    public IReadOnlySet<string> FileExtensions { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".rvt", ".rfa", ".rft", ".rte" };

    public bool SupportsVersion(string version) => true;

    public Task<HostLaunchResult> LaunchAsync(HostLaunchRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.FilePath) && !File.Exists(request.FilePath))
            throw new HostDriverException($"File not found: {request.FilePath}");

        var version = ResolveVersion(request.VersionNumber, request.FilePath);
        if (version is null)
            throw new HostDriverException("No compatible Revit version found.");

        var exePath = RevitPathResolver.FindRevitPath(version);
        if (string.IsNullOrWhiteSpace(exePath))
            throw new HostDriverException($"Revit {version} installation not found.");

        var language = string.IsNullOrWhiteSpace(request.LanguageCode)
            ? "ENU"
            : request.LanguageCode.Trim().ToUpperInvariant();
        if (!SupportedLanguageCodes.Contains(language))
            throw new HostDriverException(
                $"Unsupported language code '{language}'. Supported: {string.Join(", ", SupportedLanguageCodes)}");

        var arguments = new List<string> { "/nosplash", "/language", language };
        if (!string.IsNullOrWhiteSpace(request.FilePath))
            arguments.Add(request.FilePath);

        var process = StartProcess(HostApp.Revit, exePath, arguments);
        return Task.FromResult(new HostLaunchResult(
            HostApp.Revit,
            process.Id,
            version,
            exePath,
            language,
            arguments,
            HostLaunchCoordinator.StartDialogResolver(process.Id, ct)));
    }

    public Task<FileInfoResult> ReadFileInfoAsync(string filePath, CancellationToken ct)
    {
        using var file = RevitCompoundFile.Open(filePath);

        var basicInfo = BasicFileInfoReader.Read(file);
        var transmissionData = TransmissionDataReader.Read(file);
        var projectInformation = ProjectInformationReader.Read(file);

        using var ptStream = file.TryReadStream("Global", "PartitionTable");
        var ptDecompressed = ptStream is not null
            ? PartitionTableReader.Decompress(ptStream.ToArray())
            : [];

        return Task.FromResult<FileInfoResult>(new RevitFileInfoResult
        {
            HostApp = HostApp.Revit,
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            BasicInfo = basicInfo!,
            TransmissionData = transmissionData,
            ProjectInformation = projectInformation,
            Worksets = WorksetParser.TryParse(ptDecompressed),
            PartitionSummary = PartitionTableReader.Read(ptDecompressed),
            BrowserOrganization = BrowserOrganizationReader.Read(file)
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

    private static string? ResolveVersion(string? requestedVersion, string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(requestedVersion))
            return requestedVersion;

        var installedVersions = RevitPathResolver.GetInstalledVersions();
        if (installedVersions.Count == 0) return null;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return installedVersions.FirstOrDefault();

        var fileVersion = ReadFileVersion(filePath);
        if (fileVersion is null || !int.TryParse(fileVersion, out var fileYear))
            return installedVersions.FirstOrDefault();

        return installedVersions
            .Where(v => int.TryParse(v, out var year) && year >= fileYear)
            .OrderBy(v => v)
            .FirstOrDefault();
    }

    private static string? ReadFileVersion(string filePath)
    {
        try
        {
            using var file = RevitCompoundFile.Open(filePath);
            var info = BasicFileInfoReader.Read(file);
            if (info?.RevitVersion is null) return null;
            var match = RevitVersionPattern().Match(info.RevitVersion);
            return match.Success ? match.Value : null;
        }
        catch
        {
            return null;
        }
    }

    [GeneratedRegex(@"20\d{2}")]
    private static partial Regex RevitVersionPattern();
}
