using DevTools.TestAdapter;
using DevTools.Testing.Abstractions;

namespace DevTools.TestAdapter.Tests;

[Collection(nameof(AdapterHostTestDiscoveryCollection))]
public sealed class HostMTPRegistrationTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Register_rejects_empty_assembly_name(string assemblyFileName)
    {
        lock (StubMTPPlugin.Sync)
        {
            var previous = HostTestDiscovery.Provider;
            HostTestDiscovery.Provider = null;
            try
            {
                using var directory = new TemporaryDirectory();
                var registered = HostMtpRegistration.Register(
                    assemblyFileName,
                    "DevTools.NUnit.MTP.NUnitMTP",
                    directory.Path,
                    _ => throw new InvalidOperationException("should not load"));

                Assert.False(registered);
                Assert.NotNull(HostMtpRegistration.LastError);
                Assert.Contains("required", HostMtpRegistration.LastError!, StringComparison.OrdinalIgnoreCase);
                Assert.Null(HostTestDiscovery.Provider);
            }
            finally
            {
                HostTestDiscovery.Provider = previous;
            }
        }
    }

    [Theory]
    [InlineData(@"bin\DevTools.NUnit.MTP.dll")]
    [InlineData(@"/tmp/DevTools.NUnit.MTP.dll")]
    [InlineData(@"C:\plugins\DevTools.NUnit.MTP.dll")]
    public void Register_rejects_non_bare_assembly_names(string assemblyFileName)
    {
        lock (StubMTPPlugin.Sync)
        {
            var previous = HostTestDiscovery.Provider;
            HostTestDiscovery.Provider = null;
            try
            {
                using var directory = new TemporaryDirectory();
                var registered = HostMtpRegistration.Register(
                    assemblyFileName,
                    "DevTools.NUnit.MTP.NUnitMTP",
                    directory.Path,
                    _ => throw new InvalidOperationException("should not load"));

                Assert.False(registered);
                Assert.NotNull(HostMtpRegistration.LastError);
                Assert.Contains("bare file name", HostMtpRegistration.LastError!, StringComparison.OrdinalIgnoreCase);
                Assert.Null(HostTestDiscovery.Provider);
            }
            finally
            {
                HostTestDiscovery.Provider = previous;
            }
        }
    }

    [Fact]
    public void Register_reports_missing_plugin_assembly_without_throwing()
    {
        lock (StubMTPPlugin.Sync)
        {
            var previous = HostTestDiscovery.Provider;
            var previousMapper = HostTestDiscovery.RunMapper;
            HostTestDiscovery.Provider = null;
            HostTestDiscovery.RunMapper = null;
            try
            {
                using var directory = new TemporaryDirectory();
                var registered = HostMtpRegistration.Register(
                    "DevTools.NUnit.MTP.dll",
                    "DevTools.NUnit.MTP.NUnitMTP",
                    directory.Path,
                    _ => throw new InvalidOperationException("should not load"));

                Assert.False(registered);
                Assert.NotNull(HostMtpRegistration.LastError);
                Assert.Contains("DevTools.NUnit.MTP.dll", HostMtpRegistration.LastError!, StringComparison.Ordinal);
                Assert.Null(HostTestDiscovery.Provider);
            }
            finally
            {
                HostTestDiscovery.Provider = previous;
                HostTestDiscovery.RunMapper = previousMapper;
            }
        }
    }

    [Fact]
    public void Register_loads_stub_plugin_that_assigns_provider()
    {
        lock (StubMTPPlugin.Sync)
        {
            var previous = HostTestDiscovery.Provider;
            var previousMapper = HostTestDiscovery.RunMapper;
            HostTestDiscovery.Provider = null;
            HostTestDiscovery.RunMapper = null;
            try
            {
                using var directory = new TemporaryDirectory();
                var pluginPath = Path.Combine(directory.Path, StubMTPPlugin.AssemblyFileName);
                File.Copy(StubMTPPlugin.AssemblyPath, pluginPath);

                var registered = HostMtpRegistration.Register(
                    StubMTPPlugin.AssemblyFileName,
                    StubMTPPlugin.EntryTypeFullName,
                    directory.Path,
                    path => System.Reflection.Assembly.LoadFrom(path));

                Assert.True(registered, HostMtpRegistration.LastError);
                Assert.NotNull(HostTestDiscovery.Provider);
            }
            finally
            {
                HostTestDiscovery.Provider = previous;
                HostTestDiscovery.RunMapper = previousMapper;
            }
        }
    }

    sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"host-mtp-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}

internal static class StubMTPPlugin
{
    internal const string AssemblyFileName = "StubMTP.Plugin.dll";
    internal const string EntryTypeFullName = "DevTools.TestAdapter.Tests.StubMTPPlugin";
    internal static readonly object Sync = new();
    internal static readonly string AssemblyPath =
        typeof(StubMTPPlugin).Assembly.Location;

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public static void Register()
    {
        HostTestDiscovery.Provider = new StubHostTestDiscoverer();
    }
}
