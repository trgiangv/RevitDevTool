using DevTools.Execution.Providers.Python;
using DevTools.Hosting;

namespace DevTools.Execution.Tests;

[TestClass]

public class PytestRunnerScriptTests
{
    [TestMethod]
    public void EmbeddedRunner_DefersPytestAnnotations()
    {
        PythonEmbedded.Configure(HostApp.Revit);
        PythonEmbedded.EnsureExtracted();
        var script = PythonEmbedded.PytestRunnerScript;
        Assert.IsFalse(string.IsNullOrWhiteSpace(script));

        var firstStatement = script
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .First(line => line.Length > 0 && !line.StartsWith("#", StringComparison.Ordinal));

        Assert.AreEqual("from __future__ import annotations", firstStatement);
        Assert.Contains("import pytest", script, StringComparison.Ordinal);
    }
}
