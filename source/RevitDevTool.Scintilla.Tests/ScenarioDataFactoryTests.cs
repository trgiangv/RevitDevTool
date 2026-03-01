using RevitDevTool.Scintilla.Benchmarks.Scenarios;

namespace RevitDevTool.Scintilla.Tests;

public sealed class ScenarioDataFactoryTests
{
    [Fact]
    public void BuildMessages_WithSameSeed_IsDeterministic()
    {
        var left = ScenarioDataFactory.BuildMessages(32, 256, TokenDensity.High, structuredPayload: true, seed: 777);
        var right = ScenarioDataFactory.BuildMessages(32, 256, TokenDensity.High, structuredPayload: true, seed: 777);

        Assert.Equal(left.Count, right.Count);
        for (var i = 0; i < left.Count; i++)
            Assert.Equal(left[i], right[i]);
    }

    [Fact]
    public void BuildOrderData_WithSameSeed_IsDeterministic()
    {
        var left = ScenarioDataFactory.BuildOrderData(16, seed: 888);
        var right = ScenarioDataFactory.BuildOrderData(16, seed: 888);

        Assert.Equal(left.Count, right.Count);
        for (var i = 0; i < left.Count; i++)
        {
            Assert.Equal(left[i].OrderId, right[i].OrderId);
            Assert.Equal(left[i].Status, right[i].Status);
            Assert.Equal(left[i].Timestamp, right[i].Timestamp);
            Assert.Equal(left[i].UserId, right[i].UserId);
            Assert.Equal(left[i].Amount, right[i].Amount);
        }
    }
}
