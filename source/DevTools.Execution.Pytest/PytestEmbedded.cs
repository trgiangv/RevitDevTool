using System.IO;
using System.Reflection;

namespace DevTools.Execution.Pytest;

internal static class PytestEmbedded
{
    private const string RunnerResource = "DevTools.Execution.Pytest.Resources.scripts.PytestRunner.py";

    private static string? _cached;

    public static string PytestRunnerScript =>
        _cached ??= Load(RunnerResource);

    private static string Load(string resourceName)
    {
        var assembly = typeof(PytestEmbedded).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
