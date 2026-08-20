using System.Diagnostics;
using Nice3point.Revit.Toolkit;
using NUnit.Framework;

namespace DevTools.NUnit.SampleTests;

// Discover: host smoke. ricaun.NUnit.SampleTests links this file.

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
        Console.WriteLine(RevitApiContext.Application.VersionBuild);
        Console.WriteLine($"host-pid={Process.GetCurrentProcess().Id}");
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
        Trace.WriteLine("ERR devtools-nunit-sample-trace");
        Debug.WriteLine("ERR devtools-nunit-sample-debug");
        Assert.Pass();
    }
}
