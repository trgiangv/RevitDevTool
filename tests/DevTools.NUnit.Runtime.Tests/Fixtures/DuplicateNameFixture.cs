using NUnit.Framework;

namespace DevTools.NUnit.Runtime.Tests.Fixtures;

[TestFixture]
public sealed class DuplicateNameFixture
{
    [TestCase(1, TestName = "SharedDisplayName")]
    [TestCase(2, TestName = "SharedDisplayName")]
    public void Cases_UseSameDisplayName(int value)
    {
        Assert.That(value, Is.GreaterThan(0));
    }
}
