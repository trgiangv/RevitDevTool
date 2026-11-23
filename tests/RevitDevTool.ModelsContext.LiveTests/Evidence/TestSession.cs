namespace RevitDevTool.ModelsContext.LiveTests;

[SetUpFixture]
public sealed class TestSession
{
    [OneTimeSetUp]
    public void Start()
    {
        Evidence.StartSession();
        SafetyGuard.Reset();
    }
}
