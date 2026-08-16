using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using DevTools.Hosting;
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
            // Direct child so Process.Id is the host and ArgumentList is honored.
            // Redirect stdio to pipes we drain: a parent with redirected output
            // (MTP → Runner, or Daemon stdio) must not leak those handles into Revit,
            // or the parent hangs on ReadToEnd after the launcher exits.
            var startInfo = new ProcessStartInfo
            {
                FileName = plan.ExePath,
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(plan.ExePath) ?? Environment.CurrentDirectory,
            };
            foreach (var arg in plan.Arguments)
                startInfo.ArgumentList.Add(arg);

            // CreateProcess inherits every inheritable handle. MTP/Daemon redirect
            // Runner stdout; without clearing HANDLE_FLAG_INHERIT the host keeps
            // that pipe open and the parent hangs on ReadToEnd after Runner exits.
            Process process;
            using (StdioInheritance.Suppress())
            {
                process = Process.Start(startInfo)
                    ?? throw new InvalidOperationException($"Failed to start {plan.HostApp} process.");
            }
            process.StandardInput.Close();
            process.StandardOutput.ReadToEndAsync();
            process.StandardError.ReadToEndAsync();
            return process;
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

    private static class StdioInheritance
    {
        private const uint HandleFlagInherit = 1;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetHandleInformation(IntPtr hObject, out uint lpdwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetHandleInformation(IntPtr hObject, uint dwMask, uint dwFlags);

        public static IDisposable Suppress()
        {
            var previous = new List<(IntPtr Handle, uint Flags)>();
            foreach (var std in new[] { -10, -11, -12 })
            {
                var handle = GetStdHandle(std);
                if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                    continue;
                if (!GetHandleInformation(handle, out var flags))
                    continue;
                previous.Add((handle, flags));
                SetHandleInformation(handle, HandleFlagInherit, 0);
            }

            return new Restore(previous);
        }

        private sealed class Restore(List<(IntPtr Handle, uint Flags)> previous) : IDisposable
        {
            public void Dispose()
            {
                foreach (var entry in previous)
                    SetHandleInformation(entry.Handle, HandleFlagInherit, entry.Flags & HandleFlagInherit);
            }
        }
    }

    private sealed record HostLaunchPlan(
        HostApp HostApp,
        string Version,
        string ExePath,
        string? LanguageCode,
        IReadOnlyList<string> Arguments);
}
