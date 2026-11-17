namespace RevitDevTool.ModelsContext.LiveTests;

public abstract class RecordedTest
{
    internal static void Record(CaseItem item, Action body)
    {
        Evidence.Run(item, TestContext.CurrentContext.Test.MethodName ?? TestContext.CurrentContext.Test.Name, body,
            message => Assert.Fail(message));
    }
}
