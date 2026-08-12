using NUnit.Framework;

namespace DevTools.NUnit.Runtime.Fixtures;

[SetUpFixture]
public sealed class AssemblySetUp
{
    [OneTimeSetUp]
    public void RunAssemblyOneTimeSetUp()
    {
        Directory.CreateDirectory(AcceptanceRunContext.LogDirectory);
        AcceptanceRunContext.AppendToken("AssemblySetUp.OneTimeSetUp");
    }

    [OneTimeTearDown]
    public void RunAssemblyOneTimeTearDown() =>
        AcceptanceRunContext.AppendToken("AssemblySetUp.OneTimeTearDown");
}
