using System.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DevTools.MSTest.Civil3D.SampleTests;

[TestClass]
public sealed class HostSmokeTests
{
    [TestMethod]
    public void Arithmetic_runs_inside_civil3d_host()
    {
        var acadCore = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly =>
                string.Equals(assembly.GetName().Name, "accoremgd", StringComparison.OrdinalIgnoreCase)
                || string.Equals(assembly.GetName().Name, "AcCoreMgd", StringComparison.OrdinalIgnoreCase)
                || string.Equals(assembly.GetName().Name, "acdbmgd", StringComparison.OrdinalIgnoreCase));

        Assert.IsNotNull(
            acadCore,
            "AutoCAD core assemblies are not loaded in this process. Host tests must execute inside Civil 3D.");

        Console.WriteLine($"acad-version={Application.Version}");
        Console.WriteLine($"host-pid={Process.GetCurrentProcess().Id}");
        Console.WriteLine($"process-name={Process.GetCurrentProcess().ProcessName}");

        var civilHint = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetName().Name)
            .FirstOrDefault(name =>
                name is not null
                && name.Contains("Aecc", StringComparison.OrdinalIgnoreCase));
        if (civilHint is not null)
            Console.WriteLine($"civil-assembly={civilHint}");

        Trace.WriteLine("devtools-mstest-civil3d-trace-marker");
        Debug.WriteLine("devtools-mstest-civil3d-debug-marker");
        var pid = Process.GetCurrentProcess().Id;
        Assert.AreEqual(pid * 2, pid + pid);
    }

    [TestMethod]
    public void Writes_output()
    {
        Console.WriteLine("devtools-mstest-civil3d-sample-output");
        Trace.WriteLine("devtools-mstest-civil3d-sample-trace");
        Debug.WriteLine("devtools-mstest-civil3d-sample-debug");
    }
}
