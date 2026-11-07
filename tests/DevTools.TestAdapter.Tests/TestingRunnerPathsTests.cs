namespace DevTools.TestAdapter.Tests;

[TestClass]
public sealed class TestingRunnerPathsTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void ExpandPath_returns_null_for_blank_values(string? value)
    {
        Assert.IsNull(TestingRunnerPaths.ExpandPath(value));
    }

    [TestMethod]
    public void ExpandPath_trims_environment_variables()
    {
        var expanded = TestingRunnerPaths.ExpandPath("  %TEMP%  ");
        Assert.IsFalse(string.IsNullOrWhiteSpace(expanded));
        Assert.AreEqual(Environment.GetEnvironmentVariable("TEMP"), expanded);
    }

    [TestMethod]
    public void ReadEnvironment_returns_trimmed_value_or_null()
    {
        const string variable = "DEVTOOLS_TESTING_PATHS_" + nameof(ReadEnvironment_returns_trimmed_value_or_null);
        Environment.SetEnvironmentVariable(variable, "  value  ");
        try
        {
            Assert.AreEqual("value", TestingRunnerPaths.ReadEnvironment(variable));
            Environment.SetEnvironmentVariable(variable, "   ");
            Assert.IsNull(TestingRunnerPaths.ReadEnvironment(variable));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [TestMethod]
    public void ResolveRunnerPath_uses_configured_path_when_runnable()
    {
        var path = typeof(TestingRunnerPathsTests).Assembly.Location;
        var resolved = TestingRunnerPaths.ResolveRunnerPath(path);
        Assert.AreEqual(Path.GetFullPath(path), resolved);
    }

    [TestMethod]
    public void Missing_runner_names_the_path()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "DevTools.TestRunner.exe");

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => TestingRunnerPaths.ResolveRunnerPath(path));

        Assert.Contains(path, error.Message, StringComparison.Ordinal);
        Assert.Contains("Restore the RevitDevTool.TestAdapter package", error.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public void Blank_runner_path_asks_restore()
    {
        var error = Assert.ThrowsExactly<InvalidOperationException>(() => TestingRunnerPaths.ResolveRunnerPath(null));

        Assert.Contains("Restore the RevitDevTool.TestAdapter package", error.Message, StringComparison.Ordinal);
    }
}
