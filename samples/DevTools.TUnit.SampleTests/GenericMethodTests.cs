namespace DevTools.TUnit.SampleTests;

// Scope: generic helper methods invoked from ordinary test methods.

public sealed class GenericMethodTests
{
    [Test]
    public async Task Sum_int_via_generic_helper()
    {
        await Assert.That(Add(1, 2)).IsEqualTo(3);
    }

    [Test]
    public async Task Sum_double_via_generic_helper()
    {
        await Assert.That(Add(1.5, 2.5)).IsEqualTo(4.0).Within(1e-9);
    }

    [Test]
    public async Task Type_name_via_generic_helper()
    {
        await Assert.That(Name<int>()).IsEqualTo("System.Int32");
        await Assert.That(Name<string>()).IsEqualTo("System.String");
    }

    static int Add(int left, int right) => left + right;

    static double Add(double left, double right) => left + right;

    static string Name<T>() => typeof(T).FullName!;
}
