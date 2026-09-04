using DevTools.Hosting;
using DevTools.Logging.Diagnostics;
using DevTools.Logging.Options;
using DevTools.Logging.Targets;
using Microsoft.Extensions.Logging;

namespace DevTools.Logging.Tests;

public sealed class LogFileNamingPolicyTests
{
    [Fact]
    public void StartupTrace_uses_crash_prefix_not_log_prefix()
    {
        var dir = Directory.CreateTempSubdirectory("log-policy-").FullName;
        try
        {
            using var trace = StartupTrace.Begin("Revit", "2025", 99, dir);
            trace.Fail(new InvalidOperationException("boom"));

            var crashFile = Path.Combine(dir, "crash_Revit_2025_99.log");
            Assert.True(File.Exists(crashFile));
            Assert.DoesNotContain("log_", Path.GetFileName(crashFile), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void FileLogProcessor_uses_log_prefix_not_crash_prefix()
    {
        var dir = Directory.CreateTempSubdirectory("file-log-").FullName;
        try
        {
            using var factory = LoggerFactory.Create(static _ => { });
            var processor = new FileLogProcessor(factory, new StubHostAppInfo());
            processor.Enable(new FileLoggingOptions
            {
                Enabled = true,
                LogFolder = dir,
                Format = SaveFormat.Text,
            });

            factory.CreateLogger("policy").LogInformation("hello");

            var logFile = Directory.GetFiles(dir, "log_*").SingleOrDefault();
            Assert.NotNull(logFile);
            Assert.StartsWith("log_", Path.GetFileName(logFile), StringComparison.Ordinal);
            Assert.DoesNotContain("crash_", Path.GetFileName(logFile), StringComparison.Ordinal);

            ((IDisposable)processor).Dispose();
            factory.Dispose();
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); }
            catch { /* file lock race on Windows */ }
        }
    }

    private sealed class StubHostAppInfo : IHostAppInfo
    {
        public HostApp Host => HostApp.Revit;
        public string VersionNumber => "2025";
        public string? VersionBuild => "26.0";
        public int ProcessId => 4242;
    }
}
