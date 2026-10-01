using System.Reflection;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DevTools.MSTest.Runtime.Tests.Fixtures;

public static class Probe
{
    public static Assembly? ExecutingAssembly;
    public static string? Marker;
    public static int Executions;

#if MARKER_B
    public const string ExpectedMarker = "B";
#else
    public const string ExpectedMarker = "A";
#endif
}

[TestClass]
public class IdentityTests
{
    [TestMethod]
    public void MarksExecutingAssembly()
    {
        Probe.ExecutingAssembly = Assembly.GetExecutingAssembly();
        Probe.Marker = Probe.ExpectedMarker;
        Interlocked.Increment(ref Probe.Executions);
    }
}
