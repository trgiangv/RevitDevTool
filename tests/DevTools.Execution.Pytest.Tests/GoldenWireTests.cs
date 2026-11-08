using DevTools.Execution.External.Testing;
using DevTools.Ipc;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class GoldenWireTests
{
    [TestMethod]
    public void Run_request_roundtrips() => AssertRoundtrip<PytestRunRequest>("run_request.json");

    [TestMethod]
    public void Run_response_roundtrips() => AssertRoundtrip<PytestRunResponse>("run_response.json");

    [TestMethod]
    public void Progress_roundtrips() => AssertRoundtrip<PytestCaseResult>("progress_notification.json");

    [TestMethod]
    public void Instance_info_roundtrips() => AssertRoundtrip<InstanceInfo>("instance_info.json");

    private static void AssertRoundtrip<T>(string name)
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Golden", name));
        var value = JsonSerializer.Deserialize<T>(json);

        var expected = JsonNode.Parse(json);
        var actual = JsonNode.Parse(JsonSerializer.Serialize(value));
        Assert.IsTrue(JsonNode.DeepEquals(expected, actual), $"{name} changed on the C# side:\n{actual}");
    }
}
