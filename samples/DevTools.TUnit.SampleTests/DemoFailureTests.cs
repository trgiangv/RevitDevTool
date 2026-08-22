namespace DevTools.TUnit.SampleTests;

// Scope: deliberate failure for IDE / dotnet test red-state verification.

public sealed class DemoFailureTests
{
    [Test]
    public void Intentional_failure_for_demo()
    {
        Assert.Fail("Expected demo failure for IDE/dotnet test verification.");
    }
}
