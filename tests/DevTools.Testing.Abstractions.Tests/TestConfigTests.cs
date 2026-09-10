using DevTools.Testing.Abstractions.Config;

namespace DevTools.Testing.Abstractions.Tests;

public sealed class TestConfigTests
{
    [Fact]
    public void File_and_section_names_match_testconfig_contract()
    {
        Assert.Equal("testconfig.json", TestConfig.FileName);
        Assert.Equal("devtools", TestConfig.SectionName);
    }

    [Theory]
    [InlineData("hostName", "devtools:hostName")]
    [InlineData("hostVersion", "devtools:hostVersion")]
    [InlineData("forceLaunch", "devtools:forceLaunch")]
    [InlineData("perTestTimeoutSeconds", "devtools:perTestTimeoutSeconds")]
    [InlineData("launchTimeoutSeconds", "devtools:launchTimeoutSeconds")]
    [InlineData("runnerPath", "devtools:runnerPath")]
    [InlineData("frameworkId", "devtools:frameworkId")]
    public void Configuration_keys_prefix_devtools_section(string key, string expected)
    {
        Assert.Equal(expected, TestConfig.Keys.Configuration(key));
    }
}
