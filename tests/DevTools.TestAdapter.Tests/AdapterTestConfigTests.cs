using DevTools.TestAdapter;

namespace DevTools.TestAdapter.Tests;

[CollectionDefinition(nameof(AdapterTestConfigTests), DisableParallelization = true)]
public sealed class AdapterTestConfigTestsCollection;

[Collection(nameof(AdapterTestConfigTests))]
public sealed class AdapterTestConfigTests
{
    [Fact]
    public void TryReadPluginConfig_requires_devtools_section_fields()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "testconfig.json");
        var previous = File.Exists(configPath) ? File.ReadAllText(configPath) : null;
        File.WriteAllText(configPath, """{"devtools":{"frameworkId":"nunit"}}""");
        try
        {
            Assert.False(AdapterTestConfig.TryReadPluginConfig(out _, out var error));
            Assert.Contains("mtpAssembly", error!, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (previous is null)
                File.Delete(configPath);
            else
                File.WriteAllText(configPath, previous);
        }
    }

    [Fact]
    public void TryReadPluginConfig_reads_complete_devtools_section()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "testconfig.json");
        var previous = File.Exists(configPath) ? File.ReadAllText(configPath) : null;
        File.WriteAllText(
            configPath,
            """{"devtools":{"frameworkId":"nunit","mtpAssembly":"DevTools.NUnit.MTP.dll","mtpEntry":"Entry"}}""");
        try
        {
            Assert.True(AdapterTestConfig.TryReadPluginConfig(out var config, out var error), error);
            Assert.Equal("nunit", config!.FrameworkId);
            Assert.Equal("DevTools.NUnit.MTP.dll", config.MtpAssembly);
            Assert.Equal("Entry", config.MtpEntry);
            Assert.Equal("DevTools.NUnit.MTP.dll", AdapterTestConfig.TryReadMtpAssembly());
        }
        finally
        {
            if (previous is null)
                File.Delete(configPath);
            else
                File.WriteAllText(configPath, previous);
        }
    }
}
