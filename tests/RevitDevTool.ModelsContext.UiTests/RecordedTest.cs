using RevitDevTool.ModelsContext.LiveTests;

namespace RevitDevTool.ModelsContext.UiTests;

public abstract class RecordedTest
{
    public TestContext TestContext { get; set; } = null!;

    internal void Record(CaseItem item, Action body)
    {
        Evidence.Run(item, TestContext.TestName ?? GetType().Name, body, message => Assert.Fail(message));
    }
}
