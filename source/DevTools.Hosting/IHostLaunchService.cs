using System.Diagnostics;

namespace DevTools.Hosting;

public interface IHostLaunchService
{
    HostProcessStart Start(HostLaunchRequest request, CancellationToken cancellationToken);
}

/// <summary>OS process started from a resolved launch plan.</summary>
public sealed record HostProcessStart(
    Process Process,
    string Version,
    string ExePath,
    string? LanguageCode,
    IReadOnlyList<string> Arguments,
    StartupDialogResolverHandle? DialogResolver);
