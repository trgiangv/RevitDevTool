using DevTools.Testing.Abstractions.Config;

namespace DevTools.Testing.Abstractions.Tests;

public sealed class HostTestConfigTests
{
    [Fact]
    public void File_and_section_names_match_testconfig_contract()
    {
        Assert.Equal("testconfig.json", HostTestConfig.FileName);
        Assert.Equal("devtools", HostTestConfig.SectionName);
    }

    [Theory]
    [InlineData("hostName", "devtools:hostName")]
    [InlineData("hostVersion", "devtools:hostVersion")]
    [InlineData("forceLaunch", "devtools:forceLaunch")]
    [InlineData("perTestTimeoutSeconds", "devtools:perTestTimeoutSeconds")]
    [InlineData("launchTimeoutSeconds", "devtools:launchTimeoutSeconds")]
    [InlineData("runnerPath", "devtools:runnerPath")]
    [InlineData("frameworkId", "devtools:frameworkId")]
    [InlineData("mtpAssembly", "devtools:mtpAssembly")]
    [InlineData("mtpEntry", "devtools:mtpEntry")]
    public void Configuration_keys_prefix_devtools_section(string key, string expected)
    {
        Assert.Equal(expected, HostTestConfig.Keys.Configuration(key));
    }
}
