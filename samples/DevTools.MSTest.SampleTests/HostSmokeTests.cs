using System.Diagnostics;
using Nice3point.Revit.Toolkit;

namespace DevTools.MSTest.SampleTests;

// Scope: prove tests execute inside a live Revit process with RevitAPI loaded.

[TestClass]
public sealed class HostSmokeTests
{
    [TestMethod]
    public void Arithmetic_runs_inside_host()
    {
        var revitApi = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly =>
                string.Equals(assembly.GetName().Name, "RevitAPI", StringComparison.OrdinalIgnoreCase));
        Assert.IsNotNull(revitApi, "RevitAPI is not loaded in this process. Host tests must execute inside Revit.");
        Assert.IsTrue(string.Equals(
            Process.GetCurrentProcess().ProcessName,
            "Revit",
            StringComparison.OrdinalIgnoreCase));

        var pid = Process.GetCurrentProcess().Id;
        Console.WriteLine($"host-pid={pid}");
        Console.WriteLine(RevitApiContext.Application.VersionBuild);
        Assert.AreEqual(pid * 2, pid + pid);
    }

    [TestMethod]
    public void Writes_output()
    {
        Console.WriteLine("devtools-mstest-sample-output 3");
        Trace.WriteLine("ERR devtools-mstest-sample-trace");
        Debug.WriteLine("ERR devtools-mstest-sample-debug");
    }
}
