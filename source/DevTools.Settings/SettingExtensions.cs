using System.IO;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Settings;

/// <summary>
/// Registers path options and JSON <see cref="FileConfig"/> on <see cref="IServiceCollection"/> (same layering as host <c>ConfigureServices</c>).
/// </summary>
public static class SettingExtensions
{
    /// <summary>
    /// Configures <see cref="PathOptions"/> (content root, Settings/Logs folders) and <see cref="FileConfig"/>.
    /// </summary>
    public static IServiceCollection AddSettingServices(this IServiceCollection services, string contentRoot)
    {
        services.Configure<PathOptions>(options =>
        {
            options.RootDirectory = contentRoot;
            options.SettingsDirectory = Path.Combine(contentRoot, "Settings");
            options.LogsDirectory = Path.Combine(contentRoot, "Logs");
            options.EnsureDirectoriesExist();
        });
        services.AddSingleton<IFileConfig<PathOptions>, FileConfig>();
        return services;
    }
}

