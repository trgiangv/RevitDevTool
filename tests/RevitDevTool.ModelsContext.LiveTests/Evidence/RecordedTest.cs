namespace RevitDevTool.ModelsContext.LiveTests;

public abstract class RecordedTest
{
    internal static void Record(CaseItem item, Action body, bool writesView = false)
    {
        Evidence.Run(item, TestContext.CurrentContext.Test.MethodName ?? TestContext.CurrentContext.Test.Name, () =>
        {
            SafetyGuard.Start();
            try
            {
                body();
            }
            catch
            {
                SafetyGuard.Observe();
                throw;
            }

            SafetyGuard.Verify(writesView);
        }, message => Assert.Fail(message));
    }
}
