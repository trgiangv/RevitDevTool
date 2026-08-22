namespace DevTools.TUnit.SampleTests;

// Scope: [ClassDataSource<T>] injecting a POCO fixture per test class instance.

public sealed class SpanCase
{
    public double X { get; set; } = 3.0;
    public double Y { get; set; } = 4.0;
}

[ClassDataSource<SpanCase>]
public sealed class ClassDataSourceScalarTests(SpanCase data)
{
    [Test]
    [Category("Data")]
    public async Task Injected_class_has_positive_span()
    {
        await Assert.That(data.X).IsGreaterThan(0.0);
        await Assert.That(data.Y).IsGreaterThan(0.0);
    }
}
