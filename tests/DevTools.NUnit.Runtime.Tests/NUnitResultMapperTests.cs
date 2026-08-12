using DevTools.NUnit.Core.Results;
using DevTools.NUnit.Runtime;
using NUnit.Framework.Interfaces;

namespace DevTools.NUnit.Runtime.Tests;

public sealed class NUnitResultMapperTests
{
    [Fact]
    public void MapOutcome_MapsWarningToPassed()
    {
        Assert.Equal(NUnitOutcomes.Passed, NUnitResultMapper.MapOutcome(ResultState.Warning));
    }

    [Fact]
    public void MapOutcome_MapsCancelledLabelToCancelled()
    {
        Assert.Equal(NUnitOutcomes.Cancelled, NUnitResultMapper.MapOutcome(ResultState.Cancelled));
    }

    [Fact]
    public void MapOutcome_MapsSetupErrorToError()
    {
        Assert.Equal(NUnitOutcomes.Error, NUnitResultMapper.MapOutcome(ResultState.SetUpError));
    }
}
