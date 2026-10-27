using DevTools.Update.Apply;
using DevTools.Update.Autodesk;
using DevTools.Update.Engine;
using DevTools.Update.Installations;
using DevTools.Update.Releases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevTools.Update;

public static class UpdateServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the update engine and its scheduler. The host registers <see cref="IUpdateNotifier"/> and
    ///     the <c>IHostPathResolver</c> implementations. Use it in tray mode only (0048).
    ///     To use a private deploy server, register an <see cref="IReleaseProvider"/> before this call.
    /// </summary>
    public static IServiceCollection AddUpdateEngine(this IServiceCollection services)
    {
        services.AddOptions<UpdateEngineOptions>();
        services.TryAddSingleton<IUpdateNotifier, NullUpdateNotifier>();
        services.TryAddSingleton<IFolderLock, RestartManagerFolderLock>();
        services.TryAddSingleton<IReleaseProvider>(sp => new CachedReleaseProvider(
            new GitHubReleaseProvider(Options(sp).Repository, Options(sp).GitHubToken), TimeProvider.System));
        services.AddSingleton(sp => new AutodeskBundle(Options(sp).BundlePath ?? AutodeskBundle.DefaultRoot));
        services.AddSingleton(sp => new ReleaseReader(new HttpClient(), new ReleaseSignature(Options(sp).PublicKey), sp.GetService<ILogger<ReleaseReader>>()));
        services.AddSingleton<InstallationStore>();
        services.AddSingleton<PayloadInstaller>();
        services.AddSingleton<DaemonUpdater>();
        services.AddSingleton<UpdateEngine>();
        services.AddSingleton<UpdateScheduler>();
        services.AddHostedService(sp => sp.GetRequiredService<UpdateScheduler>());
        return services;
    }

    private static UpdateEngineOptions Options(IServiceProvider services) => services.GetRequiredService<IOptions<UpdateEngineOptions>>().Value;
}
