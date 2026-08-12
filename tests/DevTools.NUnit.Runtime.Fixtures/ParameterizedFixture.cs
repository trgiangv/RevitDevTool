using NUnit.Framework;

namespace DevTools.NUnit.Runtime.Fixtures;

[TestFixtureSource(typeof(TestData), nameof(TestData.FixtureArguments))]
public sealed class ParameterizedFixture
{
    private readonly object _value;

    public ParameterizedFixture(object value) => _value = value;

    [OneTimeSetUp]
    public void FixtureOneTimeSetUp() =>
        AcceptanceRunContext.AppendToken(
            $"ParameterizedFixture.OneTimeSetUp:{_value}");

    [OneTimeTearDown]
    public void FixtureOneTimeTearDown() =>
        AcceptanceRunContext.AppendToken(
            $"ParameterizedFixture.OneTimeTearDown:{_value}");

    [Test]
    public void FixtureSource_ValueIsPreserved()
    {
        AcceptanceRunContext.AppendToken($"ParameterizedFixture.Test:{_value}");

        switch (_value)
        {
            case int intValue:
                Assert.That(intValue, Is.EqualTo(3));
                break;
            case string stringValue:
                Assert.That(stringValue, Is.EqualTo("fixture-source"));
                break;
            default:
                Assert.Fail("unexpected fixture argument");
                break;
        }
    }

    [Test]
    public void FixtureSource_GenerationMarkerIsVisible() =>
        Assert.That(GenerationMarker.Value, Is.EqualTo("generation-one"));
}

[TestFixture(typeof(int))]
[TestFixture(typeof(string))]
public sealed class GenericFixture<T>
{
    [Test]
    public void GenericFixture_UsesRequestedType()
    {
        AcceptanceRunContext.AppendToken($"GenericFixture<{typeof(T).Name}>.Test");
        Assert.That(typeof(T), Is.Not.EqualTo(typeof(object)));
    }
}
