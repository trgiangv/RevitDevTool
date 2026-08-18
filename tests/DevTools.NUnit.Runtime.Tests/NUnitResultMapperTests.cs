using DevTools.Testing.Abstractions.Contracts;
using DevTools.NUnit.Runtime;
using NUnit.Framework.Interfaces;

namespace DevTools.NUnit.Runtime.Tests;

public sealed class NUnitResultMapperTests
{
    [Fact]
    public void MapOutcome_MapsWarningToPassed()
    {
        Assert.Equal(TestingOutcomes.Passed, NUnitResultMapper.MapOutcome(ResultState.Warning));
    }

    [Fact]
    public void MapOutcome_MapsCancelledLabelToCancelled()
    {
        Assert.Equal(TestingOutcomes.Cancelled, NUnitResultMapper.MapOutcome(ResultState.Cancelled));
    }

    [Fact]
    public void MapOutcome_MapsSetupErrorToError()
    {
        Assert.Equal(TestingOutcomes.Error, NUnitResultMapper.MapOutcome(ResultState.SetUpError));
    }
}
