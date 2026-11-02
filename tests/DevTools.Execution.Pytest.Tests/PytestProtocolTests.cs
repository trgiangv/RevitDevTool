using DevTools.Execution.External.Testing;
using System.Text.Json;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class PytestProtocolTests
{
    [TestMethod]
    public void A_client_inside_the_range_is_accepted()
    {
        Assert.IsTrue(Parse("""{"version":"0.6.0","protocol":{"current":2,"min":1}}""", out var error), error);
    }

    [TestMethod]
    public void A_client_above_the_range_is_told_to_update_RevitDevTool()
    {
        Assert.IsFalse(Parse("""{"version":"1.0.0","protocol":{"current":3,"min":2}}""", out var error));

        Assert.StartsWith("revitdevtool_pytest 1.0.0 is not compatible with RevitDevTool", error!, StringComparison.Ordinal);
        Assert.EndsWith(PytestProtocol.UpdateProductMessage, error!, StringComparison.Ordinal);
    }

    [TestMethod]
    public void A_client_below_the_range_is_told_to_update_the_package()
    {
        Assert.IsFalse(Parse("""{"version":"0.4.0","protocol":{"current":0,"min":0}}""", out var error));

        Assert.StartsWith("revitdevtool_pytest 0.4.0 is not compatible with RevitDevTool", error!, StringComparison.Ordinal);
        Assert.EndsWith(PytestProtocol.UpdateClientMessage, error!, StringComparison.Ordinal);
    }

    [TestMethod]
    public void A_client_without_a_sender_is_told_to_update_the_package()
    {
        Assert.IsFalse(Parse(sender: null, out var error));

        Assert.StartsWith("revitdevtool_pytest is not compatible with RevitDevTool", error!, StringComparison.Ordinal);
        Assert.EndsWith(PytestProtocol.UpdateClientMessage, error!, StringComparison.Ordinal);
    }

    private static bool Parse(string? sender, out string? error)
    {
        var json = sender is null
            ? """{"test_root":"C:/t","nodeids":["a.py::t"]}"""
            : $$"""{"test_root":"C:/t","nodeids":["a.py::t"],"sender":{{sender}}}""";
        using var document = JsonDocument.Parse(json);
        return PytestExecutionService.TryParseRunRequest(document.RootElement.Clone(), out _, out error);
    }
}