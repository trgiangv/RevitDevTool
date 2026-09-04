using DevTools.TestAdapter;

namespace DevTools.TestAdapter.Tests;

public sealed class TestingRunnerPathsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExpandPath_returns_null_for_blank_values(string? value)
    {
        Assert.Null(TestingRunnerPaths.ExpandPath(value));
    }

    [Fact]
    public void ExpandPath_trims_environment_variables()
    {
        var expanded = TestingRunnerPaths.ExpandPath("  %TEMP%  ");
        Assert.False(string.IsNullOrWhiteSpace(expanded));
        Assert.Equal(Environment.GetEnvironmentVariable("TEMP"), expanded);
    }

    [Fact]
    public void ReadEnvironment_returns_trimmed_value_or_null()
    {
        const string variable = "DEVTOOLS_TESTING_PATHS_" + nameof(ReadEnvironment_returns_trimmed_value_or_null);
        Environment.SetEnvironmentVariable(variable, "  value  ");
        try
        {
            Assert.Equal("value", TestingRunnerPaths.ReadEnvironment(variable));
            Environment.SetEnvironmentVariable(variable, "   ");
            Assert.Null(TestingRunnerPaths.ReadEnvironment(variable));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public void ResolveRunnerPath_uses_configured_path_when_runnable()
    {
        var path = typeof(TestingRunnerPathsTests).Assembly.Location;
        var resolved = TestingRunnerPaths.ResolveRunnerPath(path);
        Assert.Equal(Path.GetFullPath(path), resolved);
    }
}
