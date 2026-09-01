using DevTools.Execution.Providers.Python;
using DevTools.Hosting;

namespace DevTools.Execution.Tests;

public class PytestRunnerScriptTests
{
    [Fact]
    public void EmbeddedRunner_DefersPytestAnnotations()
    {
        PythonEmbedded.Configure(HostApp.Revit);
        var script = PythonEmbedded.PytestRunnerScript;
        Assert.False(string.IsNullOrWhiteSpace(script));

        var firstStatement = script
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .First(line => line.Length > 0 && !line.StartsWith("#", StringComparison.Ordinal));

        Assert.Equal("from __future__ import annotations", firstStatement);
        Assert.Contains("import pytest", script, StringComparison.Ordinal);
    }
}
