using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DevTools.TestAdapter.Tests.Fixtures;

// Bodies stay empty. Discovery must list one node per row and must not run them.

[TestClass]
public sealed class DataRowTests
{
    [TestMethod]
    [DataRow(1.0, 0.0, 0.0, DisplayName = "Unit_X")]
    [DataRow(0.0, 1.0, 0.0, DisplayName = "Unit_Y")]
    [DataRow(0.0, 0.0, 1.0, DisplayName = "Unit_Z")]
    public void Basis(double x, double y, double z)
    {
        Probe.Executions++;
    }

    [TestMethod]
    [DynamicData(nameof(Magnitudes))]
    public void Magnitude(double x, double y, double z, double expected)
    {
        Probe.Executions++;
    }

    public static IEnumerable<object[]> Magnitudes
    {
        get
        {
            yield return new object[] { 0.0, 0.0, 0.0, 0.0 };
            yield return new object[] { 0.0, 3.0, 4.0, 5.0 };
            yield return new object[] { -2.0, -3.0, -6.0, 7.0 };
        }
    }

    [TestMethod]
    [DynamicData(nameof(ClassSource.Rows), typeof(ClassSource))]
    public void FromClass(double x, double y, double expected)
    {
        Probe.Executions++;
    }
}

public static class ClassSource
{
    public static IEnumerable<object[]> Rows
    {
        get
        {
            yield return new object[] { 3.0, 4.0, 5.0 };
            yield return new object[] { 0.0, 0.0, 0.0 };
        }
    }
}
