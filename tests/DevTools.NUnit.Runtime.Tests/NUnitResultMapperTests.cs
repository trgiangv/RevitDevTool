using DevTools.Testing.Abstractions.Contracts;
using DevTools.NUnit.Runtime;
using NUnit.Framework.Interfaces;

namespace DevTools.NUnit.Runtime.Tests;

public sealed class NUnitResultMapperTests
{
    [Theory]
    [InlineData(nameof(ResultState.Success), TestOutcomes.Passed)]
    [InlineData(nameof(ResultState.Warning), TestOutcomes.Passed)]
    [InlineData(nameof(ResultState.Inconclusive), TestOutcomes.Inconclusive)]
    [InlineData(nameof(ResultState.Cancelled), TestOutcomes.Cancelled)]
    [InlineData(nameof(ResultState.Ignored), TestOutcomes.Skipped)]
    [InlineData(nameof(ResultState.Explicit), TestOutcomes.Skipped)]
    [InlineData(nameof(ResultState.Skipped), TestOutcomes.Skipped)]
    [InlineData(nameof(ResultState.Error), TestOutcomes.Error)]
    [InlineData(nameof(ResultState.SetUpError), TestOutcomes.Error)]
    [InlineData(nameof(ResultState.TearDownError), TestOutcomes.Error)]
    [InlineData(nameof(ResultState.NotRunnable), TestOutcomes.Error)]
    public void MapOutcome_maps_known_result_states(string stateName, string expected)
    {
        var state = typeof(ResultState)
            .GetField(stateName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
            .GetValue(null)!;
        Assert.Equal(expected, NUnitResultMapper.MapOutcome((ResultState)state));
    }
}
