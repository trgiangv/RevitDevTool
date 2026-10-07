using System.Text.Json;
using DevTools.Daemon.Mcp.Processes;
using DevTools.Daemon.Mcp.Tools;
using Moq;

namespace DevTools.Daemon.Tests;

[TestClass]
public sealed class CodeModeToolTests
{
    [TestMethod]
    public void InputSchema_DeclaresCodeAndReadOnlyOnly()
    {
        var tool = CodeModeTool.Create(Mock.Of<IProcessSessions>());
        var schema = tool.ProtocolTool.InputSchema;
        var names = schema.GetProperty("properties").EnumerateObject().Select(property => property.Name).ToArray();

        CollectionAssert.AreEquivalent(new[] { "code", "readOnly" }, names);
        Assert.IsFalse(names.Contains("id"));
        if (schema.TryGetProperty("additionalProperties", out var additional))
            Assert.AreEqual(JsonValueKind.False, additional.ValueKind);
    }
}
