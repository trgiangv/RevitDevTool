using DevTools.Hosting;
using DevTools.Logging.Options;
using DevTools.Logging.Targets;
using Microsoft.Extensions.Logging;

namespace DevTools.Logging.Tests;

public sealed class LogTargetProcessorTests
{
    [Fact]
    public void FileLogProcessor_json_format_disable_and_wrong_type()
    {
        var dir = Directory.CreateTempSubdirectory("file-json-").FullName;
        try
        {
            using var factory = LoggerFactory.Create(static _ => { });
            var processor = new FileLogProcessor(factory, new StubHostAppInfo());
            Assert.Throws<ArgumentException>(() => processor.Enable("not-options"));

            processor.Enable(new FileLoggingOptions
            {
                LogFolder = dir,
                Format = SaveFormat.Json,
            });
            factory.CreateLogger("json").LogInformation("payload");
            Assert.Contains(Directory.GetFiles(dir, "log_*.json"), static f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

            processor.Disable();
            processor.Enable(new FileLoggingOptions { LogFolder = dir, Format = SaveFormat.Text });
            ((IDisposable)processor).Dispose();
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); }
            catch { /* lock race */ }
        }
    }

    [Fact]
    public void HttpLogProcessor_empty_endpoint_disables_and_wrong_type()
    {
        using var factory = LoggerFactory.Create(static _ => { });
        var processor = new HttpLogProcessor(factory);
        Assert.Throws<ArgumentException>(() => processor.Enable("bad"));

        processor.Enable(new HttpLoggingOptions { Endpoint = "   " });
        processor.Enable(new HttpLoggingOptions
        {
            Endpoint = "http://127.0.0.1:9/unreachable",
            BatchSize = 0,
            Format = SaveFormat.Text,
        });
        factory.CreateLogger("http").LogWarning("batch");
        processor.Disable();
        processor.Dispose();
    }

    private sealed class StubHostAppInfo : IHostAppInfo
    {
        public HostApp Host => HostApp.Revit;
        public string VersionNumber => "2025";
        public string? VersionBuild => "26.0";
        public int ProcessId => 99;
    }
}
