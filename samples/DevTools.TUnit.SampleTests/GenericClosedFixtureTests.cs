namespace DevTools.TUnit.SampleTests;

// Scope: closed generic fixtures via [InheritsTests] on concrete derived classes.

public abstract class GenericClosedFixtureTestsBase<T>
{
    [Test]
    public async Task Closed_generic_matches_expected_type()
    {
        await Assert.That(typeof(T)).IsEqualTo(ExpectedType);
    }

    protected abstract Type ExpectedType { get; }
}

[InheritsTests]
public sealed class GenericIntFixtureTests : GenericClosedFixtureTestsBase<int>
{
    protected override Type ExpectedType => typeof(int);
}

[InheritsTests]
public sealed class GenericStringFixtureTests : GenericClosedFixtureTestsBase<string>
{
    protected override Type ExpectedType => typeof(string);
}
