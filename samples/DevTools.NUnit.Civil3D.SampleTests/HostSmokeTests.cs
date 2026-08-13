using System.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices.Core;
using NUnit.Framework;

namespace DevTools.NUnit.Civil3D.SampleTests;

[TestFixture]
public sealed class HostSmokeTests
{
    [Test]
    public void Arithmetic_runs_inside_civil3d_host()
    {
        var acadCore = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly =>
                string.Equals(assembly.GetName().Name, "accoremgd", StringComparison.OrdinalIgnoreCase)
                || string.Equals(assembly.GetName().Name, "AcCoreMgd", StringComparison.OrdinalIgnoreCase)
                || string.Equals(assembly.GetName().Name, "acdbmgd", StringComparison.OrdinalIgnoreCase));

        Assert.That(
            acadCore,
            Is.Not.Null,
            "AutoCAD core assemblies are not loaded in this process. Host tests must execute inside Civil 3D "
            + "via DevTools.NUnit (MTP or VSTest adapter), not a local NUnit runner.");

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

        Trace.WriteLine("devtools-nunit-civil3d-trace-marker");
        Debug.WriteLine("devtools-nunit-civil3d-debug-marker");
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
        Console.WriteLine("devtools-nunit-civil3d-sample-output");
        Trace.WriteLine("devtools-nunit-civil3d-sample-trace");
        Debug.WriteLine("devtools-nunit-civil3d-sample-debug");
        Assert.Pass();
    }
}
