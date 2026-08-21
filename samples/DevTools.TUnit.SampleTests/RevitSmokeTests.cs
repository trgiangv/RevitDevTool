using Autodesk.Revit.DB;
using System.Diagnostics;

namespace DevTools.TUnit.SampleTests;

public sealed class RevitSmokeTests
{
    [Test]
    public void Revit_API_executes_inside_the_Revit_process()
    {
        if (!string.Equals(Process.GetCurrentProcess().ProcessName, "Revit", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The TUnit test body did not execute inside Revit.");

        _ = typeof(ElementId).Assembly.GetName();
    }
}
