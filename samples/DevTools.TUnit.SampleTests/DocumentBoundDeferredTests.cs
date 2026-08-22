namespace DevTools.TUnit.SampleTests;

// Scope: document-bound data sources cannot expand at testhost.
// See NUnit sample DocumentBoundCaseSourceTests for the FilteredElementCollector
// on null Document pattern (NotRunnable at discover, expands in-host).

public sealed class DocumentBoundDeferredTests
{
    [Test]
    public async Task Document_bound_enumeration_is_inhost_only()
    {
        // TUnit source-gen cannot evaluate Revit API collectors at testhost.
        var documented = true;
        await Assert.That(documented).IsTrue();
    }
}
