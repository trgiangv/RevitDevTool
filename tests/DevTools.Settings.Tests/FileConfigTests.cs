using System.Text.Json;
using DevTools.Settings;
using DevTools.Settings.Configs;
using Microsoft.Extensions.Options;

namespace DevTools.Settings.Tests;

[TestClass]
public sealed class FileConfigTests
{
    [TestMethod]
    public void Save_and_Load_round_trip_general_config()
    {
        var root = Directory.CreateTempSubdirectory("file-config-").FullName;
        try
        {
            var options = Options.Create(new PathOptions
            {
                SettingsDirectory = Path.Combine(root, "Settings"),
                LogsDirectory = Path.Combine(root, "Logs"),
            });
            options.Value.EnsureDirectoriesExist();
            var config = new FileConfig(options);
            var expected = new GeneralConfig
            {
                Theme = AppTheme.Dark,
                UseHardwareRendering = false,
                EnableTelemetry = false,
            };

            config.Save(expected);
            var loaded = config.Load<GeneralConfig>();
            Assert.IsNotNull(loaded);
            Assert.AreEqual(expected.Theme, loaded.Theme);
            Assert.AreEqual(expected.UseHardwareRendering, loaded.UseHardwareRendering);
            Assert.AreEqual(expected.EnableTelemetry, loaded.EnableTelemetry);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Load_returns_null_when_file_missing_or_invalid()
    {
        var root = Directory.CreateTempSubdirectory("file-config-missing-").FullName;
        try
        {
            var options = Options.Create(new PathOptions
            {
                SettingsDirectory = Path.Combine(root, "Settings"),
                LogsDirectory = Path.Combine(root, "Logs"),
            });
            var config = new FileConfig(options);
            Assert.IsNull(config.Load<ExecutionConfig>());

            Directory.CreateDirectory(options.Value.SettingsDirectory);
            var badPath = options.Value.GetSettingsPath<ExecutionConfig>();
            File.WriteAllText(badPath, "{ not json");
            Assert.IsNull(config.Load<ExecutionConfig>());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void LogConfig_deserializes_nested_logging_options()
    {
        const string json = """
            {
              "fileLogging": { "enabled": true, "logFolder": "C:\\\\logs" },
              "traceListener": { "stackTraceDepth": 5 },
              "monitor": { "enablePrettyJson": true },
              "httpLogging": { "enabled": false }
            }
            """;

        var config = JsonSerializer.Deserialize<LogConfig>(json);
        Assert.IsNotNull(config);
        Assert.IsTrue(config.FileLogging.Enabled);
        Assert.AreEqual(@"C:\\logs", config.FileLogging.LogFolder);
        Assert.AreEqual(5, config.TraceListener.StackTraceDepth);
        Assert.IsTrue(config.Monitor.EnablePrettyJson);
        Assert.IsFalse(config.HttpLogging.Enabled);
    }
}
