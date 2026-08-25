using System.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices.Core;

namespace DevTools.TUnit.Civil3D.SampleTests;

public sealed class HostSmokeTests
{
    [Test]
    [Category("Host")]
    public async Task Arithmetic_runs_inside_civil3d_host()
    {
        var acadCore = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly =>
                string.Equals(assembly.GetName().Name, "accoremgd", StringComparison.OrdinalIgnoreCase)
                || string.Equals(assembly.GetName().Name, "AcCoreMgd", StringComparison.OrdinalIgnoreCase)
                || string.Equals(assembly.GetName().Name, "acdbmgd", StringComparison.OrdinalIgnoreCase));

        await Assert.That(acadCore).IsNotNull();

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

        Trace.WriteLine("devtools-tunit-civil3d-trace-marker");
        Debug.WriteLine("devtools-tunit-civil3d-debug-marker");
        var sum = 2 + 2;
        await Assert.That(sum).IsEqualTo(4);
    }

    [Test]
    [Category("Host")]
    public void Writes_output()
    {
        Console.WriteLine("devtools-tunit-civil3d-sample-output");
        Trace.WriteLine("devtools-tunit-civil3d-sample-trace");
        Debug.WriteLine("devtools-tunit-civil3d-sample-debug");
    }
}
