using System.Text.Json;
using DevTools.Execution.External.Testing;

namespace DevTools.Execution.Tests;

[TestClass]

public class PytestCaseResultStdoutTests
{
    [TestMethod]
    public void RoundtripsStdoutOnTheWire()
    {
        const string json =
            """{"nodeid":"t.py::T::a","outcome":"passed","phase":"call","duration_ms":1.5,"stdout":"Project1\n","stderr":"","message":"","traceback":""}""";
        var parsed = JsonSerializer.Deserialize<PytestCaseResult>(json);
        Assert.IsNotNull(parsed);
        Assert.AreEqual("Project1\n", parsed.Stdout);

        var element = JsonSerializer.SerializeToElement(parsed);
        Assert.AreEqual("Project1\n", element.GetProperty("stdout").GetString());
    }

    [TestMethod]
    public void RoundtripsSkippedSetupOnTheWire()
    {
        const string json =
            """{"nodeid":"t.py::test_a","outcome":"skipped","phase":"setup","duration_ms":0.1,"stdout":"","stderr":"","message":"fixture skip","traceback":""}""";
        var parsed = JsonSerializer.Deserialize<PytestCaseResult>(json);
        Assert.IsNotNull(parsed);
        Assert.AreEqual("skipped", parsed.Outcome);
        Assert.AreEqual("setup", parsed.Phase);
        Assert.AreEqual("fixture skip", parsed.Message);
    }
}
