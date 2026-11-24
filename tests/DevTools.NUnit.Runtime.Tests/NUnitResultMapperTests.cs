using DevTools.Testing.Abstractions.Contracts;
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

    [TestMethod]
    public void MapOutcome_keeps_an_assertion_failure_as_failed()
    {
        Assert.AreEqual(TestOutcomes.Failed, NUnitResultMapper.MapOutcome(ResultState.Failure));
    }

    [TestMethod]
    public void Inconclusive_explicit_test_has_no_skip_reason()
    {
        Assert.IsNull(NUnitResultMapper.MapSkipReason(ExplicitTest(), ResultState.Inconclusive));
    }

    [TestMethod]
    public void Skipped_explicit_test_keeps_its_reason()
    {
        Assert.AreEqual(ExplicitReason, NUnitResultMapper.MapSkipReason(ExplicitTest(), ResultState.Explicit));
    }

    private const string ExplicitReason = "Run by the script first.";

    private static ITest ExplicitTest()
    {
        var test = new global::NUnit.Framework.Internal.TestMethod(
            new global::NUnit.Framework.Internal.MethodWrapper(typeof(NUnitResultMapperTests), nameof(MapOutcome_keeps_an_assertion_failure_as_failed)));
        test.RunState = RunState.Explicit;
        test.Properties.Set(global::NUnit.Framework.Internal.PropertyNames.SkipReason, ExplicitReason);
        return test;
    }
}
