using System.Diagnostics;
using System.Text;
using Nice3point.Revit.Toolkit;
using NUnit.Framework;

namespace DevTools.NUnit.SampleTests;

[TestFixture]
public sealed class HostSmokeTests
{
    [Test]
    public void Arithmetic_runs_inside_host()
    {
        var revitApi = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly =>
                string.Equals(assembly.GetName().Name, "RevitAPI", StringComparison.OrdinalIgnoreCase));

        Assert.That(
            revitApi,
            Is.Not.Null,
            "RevitAPI is not loaded in this process. Host tests must execute inside Revit via DevTools.NUnit "
            + "(MTP or VSTest adapter), not a local NUnit runner.");

        //TaskDialog.Show("HostSmokeTests", "This test is running inside Revit. Click OK to continue.");
        var valuetest = new StringBuilder();
        // 100 iterations of string concatenation
        for (var i = 0; i < 100; i++)
        {
            valuetest.Append("devtools-nunit-trace-marker\n");
        }
        Console.WriteLine(RevitApiContext.Application.VersionBuild);
        Console.WriteLine($"host-pid={Process.GetCurrentProcess().Id}");
        Console.WriteLine(valuetest.ToString());
        Trace.WriteLine("devtools-nunit-trace-marker");
        Debug.WriteLine("devtools-nunit-debug-marker");
        Assert.That(2 + 2, Is.EqualTo(4));
    }

    [Test]
    public void Intentional_failure_for_demo()
    {
        Assert.Fail("Expected demo failure for IDE/dotnet test verification.");
    }

    [Test]
    public void Writes_output()
    {
        Console.WriteLine("devtools-nunit-sample-output");
        Trace.WriteLine("devtools-nunit-sample-trace");
        Debug.WriteLine("ERR devtools-nunit-sample-debug");
        Assert.Pass();
    }
}
