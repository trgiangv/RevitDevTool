using NUnit.Framework;

namespace DevTools.NUnit.Runtime.Fixtures;

[TestFixture]
public sealed class SetNameCaseSourceFixture
{
    public static IEnumerable<TestCaseData> Cases()
    {
        yield return new TestCaseData(1).SetName("Renamed_one");
        yield return new TestCaseData(2).SetName("Renamed_two");
    }

    [TestCaseSource(nameof(Cases))]
    public void Original_method(int value) => Assert.That(value, Is.GreaterThan(0));
}
