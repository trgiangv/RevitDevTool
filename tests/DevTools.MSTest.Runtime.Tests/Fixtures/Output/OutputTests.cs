using System;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DevTools.MSTest.Runtime.Tests.Fixtures;

[TestClass]
public class OutputTests
{
    [TestMethod]
    public void Writes()
    {
        Console.Out.WriteLine("stdout-probe");
        Console.Error.WriteLine("stderr-probe");
        Trace.WriteLine("trace-probe");
    }

    [TestMethod]
    [DataRow(1.0, 0.0, 0.0, DisplayName = "Unit_X")]
    [DataRow(0.0, 1.0, 0.0, DisplayName = "Unit_Y")]
    [DataRow(0.0, 0.0, 1.0, DisplayName = "Unit_Z")]
    public void Basis(double x, double y, double z)
    {
        Console.WriteLine("row:" + x.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
            + "," + y.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
            + "," + z.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
        Trace.WriteLine("trace-basis:" + x.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
            + "," + y.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
            + "," + z.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
    }
}
