using NUnit.Framework;

namespace DevTools.NUnit.Runtime.Fixtures;

[TestFixture]
public sealed class TestNameCaseFixture
{
    [TestCase(1, TestName = "Named_one")]
    [TestCase(2, TestName = "Named_two")]
    public void Original_named(int value) => Assert.That(value, Is.GreaterThan(0));
}
