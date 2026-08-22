using System.Diagnostics;

namespace DevTools.TUnit.SampleTests;

// Scope: prove tests execute inside a live Revit process with RevitAPI loaded.

public sealed class HostSmokeTests
{
    [Test]
    [Category("Host")]
    public async Task Arithmetic_runs_inside_host()
    {
        var revitApi = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly =>
                string.Equals(assembly.GetName().Name, "RevitAPI", StringComparison.OrdinalIgnoreCase));
        await Assert.That(revitApi).IsNotNull();
        await Assert.That(string.Equals(
            Process.GetCurrentProcess().ProcessName,
            "Revit",
            StringComparison.OrdinalIgnoreCase)).IsTrue();

        Console.WriteLine($"host-pid={Process.GetCurrentProcess().Id}");
        _ = typeof(ElementId).Assembly.GetName();
        var sum = 2 + 2;
        await Assert.That(sum).IsEqualTo(4);
    }

    [Test]
    [Category("Host")]
    public void Writes_output()
    {
        Console.WriteLine("devtools-tunit-sample-output");
        Trace.WriteLine("ERR devtools-tunit-sample-trace");
        Debug.WriteLine("ERR devtools-tunit-sample-debug");
    }
}
