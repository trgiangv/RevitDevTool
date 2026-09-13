using DevTools.Settings;
using DevTools.Settings.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DevTools.Settings.Tests;

[TestClass]
public sealed class PathOptionsTests
{
    [TestMethod]
    public void GetSettingsPath_uses_type_name_and_settings_directory()
    {
        var options = new PathOptions { SettingsDirectory = @"C:\root\Settings" };
        Assert.AreEqual(@"C:\root\Settings\GeneralConfig.json", options.GetSettingsPath<GeneralConfig>());
    }

    [TestMethod]
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
            Assert.IsTrue(Directory.Exists(options.SettingsDirectory));
            Assert.IsTrue(Directory.Exists(options.LogsDirectory));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void AddSettingServices_registers_path_options_and_file_config()
    {
        var root = Directory.CreateTempSubdirectory("setting-services-").FullName;
        try
        {
            var services = new ServiceCollection();
            services.AddSettingServices(root);
            using var provider = services.BuildServiceProvider();

            var options = provider.GetRequiredService<IOptions<PathOptions>>().Value;
            Assert.AreEqual(Path.Combine(root, "Settings"), options.SettingsDirectory);
            Assert.AreEqual(Path.Combine(root, "Logs"), options.LogsDirectory);
            Assert.IsTrue(Directory.Exists(options.SettingsDirectory));
            Assert.IsTrue(Directory.Exists(options.LogsDirectory));
            Assert.IsNotNull(provider.GetService<IFileConfig<PathOptions>>());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
