using DevTools.Settings;
using DevTools.Settings.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DevTools.Settings.Tests;

public sealed class PathOptionsTests
{
    [Fact]
    public void GetSettingsPath_uses_type_name_and_settings_directory()
    {
        var options = new PathOptions { SettingsDirectory = @"C:\root\Settings" };
        Assert.Equal(@"C:\root\Settings\GeneralConfig.json", options.GetSettingsPath<GeneralConfig>());
    }

    [Fact]
    public void EnsureDirectoriesExist_creates_settings_and_logs_folders()
    {
        var root = Directory.CreateTempSubdirectory("path-options-").FullName;
        try
        {
            var options = new PathOptions
            {
                SettingsDirectory = Path.Combine(root, "Settings"),
                LogsDirectory = Path.Combine(root, "Logs"),
            };
            options.EnsureDirectoriesExist();
            Assert.True(Directory.Exists(options.SettingsDirectory));
            Assert.True(Directory.Exists(options.LogsDirectory));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AddSettingServices_registers_path_options_and_file_config()
    {
        var root = Directory.CreateTempSubdirectory("setting-services-").FullName;
        try
        {
            var services = new ServiceCollection();
            services.AddSettingServices(root);
            using var provider = services.BuildServiceProvider();

            var options = provider.GetRequiredService<IOptions<PathOptions>>().Value;
            Assert.Equal(Path.Combine(root, "Settings"), options.SettingsDirectory);
            Assert.Equal(Path.Combine(root, "Logs"), options.LogsDirectory);
            Assert.True(Directory.Exists(options.SettingsDirectory));
            Assert.True(Directory.Exists(options.LogsDirectory));
            Assert.NotNull(provider.GetService<IFileConfig<PathOptions>>());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
