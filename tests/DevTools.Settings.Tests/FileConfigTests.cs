using System.Text.Json;
using DevTools.Settings;
using DevTools.Settings.Configs;
using Microsoft.Extensions.Options;

namespace DevTools.Settings.Tests;

public sealed class FileConfigTests
{
    [Fact]
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
            Assert.NotNull(loaded);
            Assert.Equal(expected.Theme, loaded.Theme);
            Assert.Equal(expected.UseHardwareRendering, loaded.UseHardwareRendering);
            Assert.Equal(expected.EnableTelemetry, loaded.EnableTelemetry);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
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
            Assert.Null(config.Load<ExecutionConfig>());

            Directory.CreateDirectory(options.Value.SettingsDirectory);
            var badPath = options.Value.GetSettingsPath<ExecutionConfig>();
            File.WriteAllText(badPath, "{ not json");
            Assert.Null(config.Load<ExecutionConfig>());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
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
        Assert.NotNull(config);
        Assert.True(config.FileLogging.Enabled);
        Assert.Equal(@"C:\\logs", config.FileLogging.LogFolder);
        Assert.Equal(5, config.TraceListener.StackTraceDepth);
        Assert.True(config.Monitor.EnablePrettyJson);
        Assert.False(config.HttpLogging.Enabled);
    }
}
