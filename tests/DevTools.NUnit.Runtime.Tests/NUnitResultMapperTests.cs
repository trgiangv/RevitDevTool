using DevTools.Testing.Abstractions.Contracts;
using DevTools.NUnit.Runtime;
using NUnit.Framework.Interfaces;

namespace DevTools.NUnit.Runtime.Tests;

[TestClass]
public sealed class NUnitResultMapperTests
{
    [TestMethod]
    [DataRow(nameof(ResultState.Success), TestOutcomes.Passed)]
    [DataRow(nameof(ResultState.Warning), TestOutcomes.Passed)]
    [DataRow(nameof(ResultState.Inconclusive), TestOutcomes.Inconclusive)]
    [DataRow(nameof(ResultState.Cancelled), TestOutcomes.Cancelled)]
    [DataRow(nameof(ResultState.Ignored), TestOutcomes.Skipped)]
    [DataRow(nameof(ResultState.Explicit), TestOutcomes.Skipped)]
    [DataRow(nameof(ResultState.Skipped), TestOutcomes.Skipped)]
    [DataRow(nameof(ResultState.Error), TestOutcomes.Error)]
    [DataRow(nameof(ResultState.SetUpError), TestOutcomes.Error)]
    [DataRow(nameof(ResultState.TearDownError), TestOutcomes.Error)]
    [DataRow(nameof(ResultState.NotRunnable), TestOutcomes.Error)]
    public void MapOutcome_maps_known_result_states(string stateName, string expected)
    {
        var state = typeof(ResultState)
            .GetField(stateName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
            .GetValue(null)!;
        Assert.AreEqual(expected, NUnitResultMapper.MapOutcome((ResultState)state));
    }
}
