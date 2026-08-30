using System.Text.Json;
using DevTools.Execution.External.Testing;

namespace DevTools.Execution.Tests;

public class PytestCaseResultStdoutTests
{
    [Fact]
    public void RoundtripsStdoutOnTheWire()
    {
        const string json =
            """{"nodeid":"t.py::T::a","outcome":"passed","phase":"call","duration_ms":1.5,"stdout":"Project1\n","stderr":"","message":"","traceback":""}""";
        var parsed = JsonSerializer.Deserialize<PytestCaseResult>(json);
        Assert.NotNull(parsed);
        Assert.Equal("Project1\n", parsed.Stdout);

        var element = JsonSerializer.SerializeToElement(parsed);
        Assert.Equal("Project1\n", element.GetProperty("stdout").GetString());
    }

    [Fact]
    public void RoundtripsSkippedSetupOnTheWire()
    {
        const string json =
            """{"nodeid":"t.py::test_a","outcome":"skipped","phase":"setup","duration_ms":0.1,"stdout":"","stderr":"","message":"fixture skip","traceback":""}""";
        var parsed = JsonSerializer.Deserialize<PytestCaseResult>(json);
        Assert.NotNull(parsed);
        Assert.Equal("skipped", parsed.Outcome);
        Assert.Equal("setup", parsed.Phase);
        Assert.Equal("fixture skip", parsed.Message);
    }
}
