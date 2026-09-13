using DevTools.TestRunner.Parsing;

namespace DevTools.TestRunner.Tests;

[TestClass]
public sealed class RunnerCommandContextTests
{
    [TestMethod]
    public void TryCreate_accepts_a_valid_host_context()
    {
        var created = RunnerCommandContext.TryCreate(
            " Revit ",
            " 2026 ",
            true,
            60,
            180,
            false,
            42,
            requestTimeoutSeconds: 0,
            out var context,
            out var error);

        Assert.IsTrue(created, error);
        Assert.IsNotNull(context);
        Assert.AreEqual("Revit", context.HostName);
        Assert.AreEqual("2026", context.HostVersion);
        Assert.IsTrue(context.ForceLaunch);
        Assert.IsTrue(context.Debug);
        Assert.AreEqual(42, context.DebugParentPid);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void TryCreate_requires_host_name(string hostName)
    {
        var created = RunnerCommandContext.TryCreate(
            hostName,
            "2026",
            false,
            60,
            180,
            false,
            null,
            requestTimeoutSeconds: 0,
            out _,
            out var error);

        Assert.IsFalse(created);
        Assert.AreEqual("Host name is required.", error);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void TryCreate_requires_host_version(string hostVersion)
    {
        var created = RunnerCommandContext.TryCreate(
            "Revit",
            hostVersion,
            false,
            60,
            180,
            false,
            null,
            requestTimeoutSeconds: 0,
            out _,
            out var error);

        Assert.IsFalse(created);
        Assert.AreEqual("Host version is required.", error);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void TryCreate_rejects_non_positive_debug_parent_pid(int debugParentPid)
    {
        var created = RunnerCommandContext.TryCreate(
            "Revit",
            "2026",
            false,
            60,
            180,
            false,
            debugParentPid,
            requestTimeoutSeconds: 0,
            out _,
            out var error);

        Assert.IsFalse(created);
        Assert.AreEqual("Debug parent pid requires a positive process id.", error);
    }

    [TestMethod]
    public void TryCreate_rejects_non_positive_per_test_timeout()
    {
        var created = RunnerCommandContext.TryCreate(
            "Revit", "2026", false, 0, 180, false, null, 0, out _, out var error);

        Assert.IsFalse(created);
        Assert.AreEqual("Per-test timeout must be positive.", error);
    }

    [TestMethod]
    public void TryCreate_rejects_negative_request_timeout()
    {
        var created = RunnerCommandContext.TryCreate(
            "Revit", "2026", false, 60, 180, false, null, -1, out _, out var error);

        Assert.IsFalse(created);
        Assert.AreEqual("Request timeout cannot be negative.", error);
    }
}
