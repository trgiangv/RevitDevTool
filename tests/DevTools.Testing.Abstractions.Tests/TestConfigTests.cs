using DevTools.Testing.Abstractions.Config;

namespace DevTools.Testing.Abstractions.Tests;

[TestClass]
public sealed class TestConfigTests
{
    [TestMethod]
    public void File_and_section_names_match_testconfig_contract()
    {
#pragma warning disable MSTEST0032 // const contract assertions document the file/section names.
        Assert.AreEqual("testconfig.json", TestConfig.FileName);
        Assert.AreEqual("devtools", TestConfig.SectionName);
#pragma warning restore MSTEST0032
    }

    [TestMethod]
    [DataRow("hostName", "devtools:hostName")]
    [DataRow("hostVersion", "devtools:hostVersion")]
    [DataRow("forceLaunch", "devtools:forceLaunch")]
    [DataRow("perTestTimeoutSeconds", "devtools:perTestTimeoutSeconds")]
    [DataRow("launchTimeoutSeconds", "devtools:launchTimeoutSeconds")]
    [DataRow("runnerPath", "devtools:runnerPath")]
    [DataRow("frameworkId", "devtools:frameworkId")]
    public void Configuration_keys_prefix_devtools_section(string key, string expected)
    {
        Assert.AreEqual(expected, TestConfig.Keys.Configuration(key));
    }
}
