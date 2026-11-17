namespace RevitDevTool.ModelsContext.LiveTests;

[TestFixture]
public sealed class VisibilityTests : RecordedTest
{
    public static IEnumerable<TestCaseData> Rows => Areas.Rows(Areas.Visibility);

    [OneTimeSetUp]
    public void Open()
    {
        VisibilityRun.Open();
    }

    [TestCaseSource(nameof(Rows))]
    public void VisibilityMatchesApi(string id)
    {
        var item = Areas.Find(Areas.Visibility, id);
        Record(item, () => VisibilityRun.Check(item));
    }
}
