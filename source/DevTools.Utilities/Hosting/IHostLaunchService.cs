using System.Diagnostics;
using DevTools.Hosting;

namespace DevTools.Utilities.Hosting;

public interface IHostLaunchService
{
    HostProcessStart Start(
        HostApp hostApp,
        string? version,
        string? languageCode,
        string? filePath,
        CancellationToken cancellationToken);
}

/// <summary>OS process started from a resolved launch plan.</summary>
public sealed record HostProcessStart(
    Process Process,
    string Version,
    string ExePath,
    string? LanguageCode,
    IReadOnlyList<string> Arguments,
    Task<StartupDialogResolverResult>? DialogResolver);
