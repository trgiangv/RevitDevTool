using DevTools.Testing.Abstractions.Contracts;
using DevTools.NUnit.Runtime;
using NUnit.Framework.Interfaces;

namespace DevTools.NUnit.Runtime.Tests;

public sealed class NUnitResultMapperTests
{
    [Theory]
    [InlineData(nameof(ResultState.Success), TestingOutcomes.Passed)]
    [InlineData(nameof(ResultState.Warning), TestingOutcomes.Passed)]
    [InlineData(nameof(ResultState.Inconclusive), TestingOutcomes.Inconclusive)]
    [InlineData(nameof(ResultState.Cancelled), TestingOutcomes.Cancelled)]
    [InlineData(nameof(ResultState.Ignored), TestingOutcomes.Skipped)]
    [InlineData(nameof(ResultState.Explicit), TestingOutcomes.Skipped)]
    [InlineData(nameof(ResultState.Skipped), TestingOutcomes.Skipped)]
    [InlineData(nameof(ResultState.Error), TestingOutcomes.Error)]
    [InlineData(nameof(ResultState.SetUpError), TestingOutcomes.Error)]
    [InlineData(nameof(ResultState.TearDownError), TestingOutcomes.Error)]
    [InlineData(nameof(ResultState.NotRunnable), TestingOutcomes.Error)]
    public void MapOutcome_maps_known_result_states(string stateName, string expected)
    {
        var state = typeof(ResultState)
            .GetField(stateName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
            .GetValue(null)!;
        Assert.Equal(expected, NUnitResultMapper.MapOutcome((ResultState)state));
    }
}
