using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.MTP;

namespace DevTools.Testing.Abstractions.Tests;

public sealed class HostMTPRegistrationTests
{
    [Theory]
    [InlineData("nunit", "DevTools.NUnit.MTP.dll", "DevTools.NUnit.MTP.NUnitMTP")]
    [InlineData("NUnit", "DevTools.NUnit.MTP.dll", "DevTools.NUnit.MTP.NUnitMTP")]
    [InlineData("tunit", "DevTools.TUnit.MTP.dll", "DevTools.TUnit.MTP.TUnitMTP")]
    [InlineData("TUnit", "DevTools.TUnit.MTP.dll", "DevTools.TUnit.MTP.TUnitMTP")]
    public void TryResolvePlugin_maps_supported_frameworks(
        string frameworkId,
        string assemblyFileName,
        string entryType)
    {
        Assert.True(HostMTPRegistration.TryResolvePlugin(frameworkId, out var plugin));
        Assert.Equal(assemblyFileName, plugin.AssemblyFileName);
        Assert.Equal(entryType, plugin.EntryTypeFullName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("xunit")]
    public void TryResolvePlugin_rejects_unsupported_frameworks(string frameworkId)
    {
        Assert.False(HostMTPRegistration.TryResolvePlugin(frameworkId, out _));
    }

    [Fact]
    public void Register_reports_missing_plugin_assembly_without_throwing()
    {
        using var directory = new TemporaryDirectory();
        var registered = HostMTPRegistration.RegisterForFramework(
            "nunit",
            directory.Path,
            _ => throw new InvalidOperationException("should not load"));

        Assert.False(registered);
        Assert.Null(HostTestDiscovery.Provider);
        Assert.Null(HostMTPRegistration.LastError);
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
