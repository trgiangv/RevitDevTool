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
    /// </summary>
    public static IServiceCollection AddUpdateEngine(this IServiceCollection services)
    {
        services.AddOptions<UpdateEngineOptions>();
        services.TryAddSingleton<IUpdateNotifier, NullUpdateNotifier>();
        services.TryAddSingleton<IFolderLock, RestartManagerFolderLock>();
        services.AddSingleton(sp => new AutodeskBundles(Options(sp).PluginsPath ?? AutodeskBundles.DefaultRoot));
        services.AddSingleton(sp => new ReleaseReader(new HttpClient(), new ReleaseSignature(Options(sp).PublicKey), sp.GetService<ILogger<ReleaseReader>>()));
        services.AddSingleton<IChannelIndexSource>(sp => new CachedChannelIndexSource(
            new ChannelIndexSource(sp.GetRequiredService<ReleaseReader>(), Options(sp).ChannelIndexUrl), TimeProvider.System));
        services.AddSingleton(sp => new InstallationStore(Options(sp).InstallationPath ?? InstallationStore.DefaultPath));
        services.AddSingleton<PayloadInstaller>();
        services.AddSingleton<DaemonUpdater>();
        services.AddSingleton<UpdateEngine>();
        services.AddSingleton<UpdateScheduler>();
        services.AddHostedService(sp => sp.GetRequiredService<UpdateScheduler>());
        return services;
    }

    private static UpdateEngineOptions Options(IServiceProvider services) => services.GetRequiredService<IOptions<UpdateEngineOptions>>().Value;
}
