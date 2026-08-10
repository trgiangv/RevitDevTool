using System.Diagnostics;
using System.Text.RegularExpressions;
using DevTools.Logging;
using DevTools.Utilities.Hosting.Resolver;
using DevTools.FileMetadata.Revit;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace DevTools.Utilities.Hosting;

// ReSharper disable once PartialTypeWithSinglePart
public sealed partial class HostLaunchService : IHostLaunchService
{
    public HostProcessStart Start(
        HostApp hostApp,
        string? version,
        string? languageCode,
        string? filePath,
        CancellationToken cancellationToken)
    {
        var plan = Resolve(hostApp, version, languageCode, filePath);
        var process = StartProcess(plan);
        var dialogTask = HostLaunchCoordinator.StartDialogResolver(hostApp, process.Id, cancellationToken);
        return new HostProcessStart(
            process,
            plan.Version,
            plan.ExePath,
            plan.LanguageCode,
            plan.Arguments,
            dialogTask);
    }

    private static HostLaunchPlan Resolve(
        HostApp hostApp,
        string? version,
        string? languageCode,
        string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath) && !File.Exists(filePath))
            throw new InvalidOperationException($"File not found: {filePath}");

        if (hostApp == HostApp.Revit)
            return ResolveRevit(version, languageCode, filePath);

        if (hostApp.IsAcadFamily())
            return ResolveAcad(hostApp, version, filePath);

        throw new InvalidOperationException($"Launch not yet supported for {hostApp}.");
    }

    private static Process StartProcess(HostLaunchPlan plan)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = plan.ExePath,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(plan.ExePath) ?? Environment.CurrentDirectory,
            };
            foreach (var arg in plan.Arguments)
                startInfo.ArgumentList.Add(arg);

            return Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start {plan.HostApp} process.");
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to launch {plan.HostApp}: {ex.Message}", ex);
        }
    }

    #region Revit

    private static readonly HashSet<string> SupportedLanguageCodes =
    [
        "ENU", "ENG", "FRA", "DEU", "ITA", "JPN", "KOR",
        "PLK", "ESP", "CHS", "CHT", "PTB", "RUS", "CSY", "HUN"
    ];

    private static HostLaunchPlan ResolveRevit(
        string? requestedVersion, string? languageCode, string? filePath)
    {
        var version = ResolveRevitVersion(requestedVersion, filePath)
            ?? throw new InvalidOperationException("No compatible Revit version found.");

        var exePath = RevitPathResolver.FindRevitPath(version);
        if (string.IsNullOrWhiteSpace(exePath))
            throw new InvalidOperationException($"Revit {version} installation not found.");

        var language = string.IsNullOrWhiteSpace(languageCode)
            ? "ENU"
            : languageCode!.Trim().ToUpperInvariant();
        if (!SupportedLanguageCodes.Contains(language))
            throw new InvalidOperationException(
                $"Unsupported language code '{language}'. Supported: {string.Join(", ", SupportedLanguageCodes)}");

        var arguments = new List<string> { "/nosplash", "/language", language };
        if (!string.IsNullOrWhiteSpace(filePath))
            arguments.Add(filePath!);

        return new HostLaunchPlan(HostApp.Revit, version, exePath!, language, arguments);
    }

    private static string? ResolveRevitVersion(string? requestedVersion, string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(requestedVersion))
            return requestedVersion;

        var installedVersions = RevitPathResolver.GetInstalledVersions();
        if (installedVersions.Count == 0) return null;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return installedVersions.FirstOrDefault();

        var fileVersion = ReadRevitFileVersion(filePath!);
        if (fileVersion is null || !int.TryParse(fileVersion, out var fileYear))
            return installedVersions.FirstOrDefault();

        return installedVersions
            .Where(v => int.TryParse(v, out var y) && y >= fileYear)
            .OrderBy(v => v)
            .ToArray()
            .FirstOrDefault();
    }

    private static string? ReadRevitFileVersion(string filePath)
    {
        var revitVersion = RevitFileMetadataReader.TryReadRevitVersion(filePath);
        if (revitVersion is null) return null;
        var match = RevitVersionRegex.Match(revitVersion);
        return match.Success ? match.Value : null;
    }

#if NET7_0_OR_GREATER
    [GeneratedRegex(@"20\d{2}")]
    private static partial Regex RevitVersionPattern();
    private static readonly Regex RevitVersionRegex = RevitVersionPattern();
#else
    private static readonly Regex RevitVersionRegex = new(@"20\d{2}", RegexOptions.Compiled);
#endif

    #endregion

    #region AutoCAD family

    private static HostLaunchPlan ResolveAcad(
        HostApp hostApp, string? requestedVersion, string? filePath)
    {
        var version = ResolveAcadVersion(requestedVersion)
            ?? throw new InvalidOperationException("No AutoCAD-family installation found.");

        var exePath = AcadPathResolver.FindAcadPath(version, hostApp);
        if (string.IsNullOrWhiteSpace(exePath))
            throw new InvalidOperationException($"{hostApp} {version} installation not found.");

        var arguments = new List<string> { "/nologo" };
        if (!string.IsNullOrWhiteSpace(filePath))
            arguments.Add(filePath!);

        return new HostLaunchPlan(hostApp, version, exePath!, null, arguments);
    }

    private static string? ResolveAcadVersion(string? requestedVersion)
    {
        if (!string.IsNullOrWhiteSpace(requestedVersion))
            return requestedVersion;

        var installed = AcadPathResolver.GetInstalledVersions();
        return installed.FirstOrDefault();
    }

    #endregion

    private sealed record HostLaunchPlan(
        HostApp HostApp,
        string Version,
        string ExePath,
        string? LanguageCode,
        IReadOnlyList<string> Arguments);
}
