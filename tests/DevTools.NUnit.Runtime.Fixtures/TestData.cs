using System.Collections;
using NUnit.Framework;

namespace DevTools.NUnit.Runtime.Fixtures;

public static class AcceptanceRunContext
{
    public const string RunIdEnvironmentVariable = "DEVTOOLS_NUNIT_ACCEPTANCE_RUN_ID";

    /// <summary>
    /// Deterministic run identifier shared with Runtime acceptance tests.
    /// Runtime sets <see cref="RunIdEnvironmentVariable"/> before execution;
    /// standalone discovery/build uses <c>default</c>.
    /// </summary>
    public static string RunId =>
        Environment.GetEnvironmentVariable(RunIdEnvironmentVariable) is { Length: > 0 } runId
            ? runId
            : "default";

    public static string LogDirectory =>
        Path.Combine(Path.GetTempPath(), "DevTools", "NUnitAcceptance");

    public static string LogPath =>
        Path.Combine(LogDirectory, RunId + ".log");

    public static void AppendToken(string token)
    {
        Directory.CreateDirectory(LogDirectory);
        File.AppendAllText(LogPath, token + Environment.NewLine);
    }

    public static IReadOnlyList<string> ReadTokens() =>
        File.Exists(LogPath)
            ? File.ReadAllLines(LogPath)
            : Array.Empty<string>();

    public static int IndexOfToken(string token)
    {
        var tokens = ReadTokens();
        for (var index = 0; index < tokens.Count; index++)
        {
            if (tokens[index] == token)
            {
                return index;
            }
        }

        return -1;
    }
}

public static class TestData
{
    public static int ExecutableSourceInvocationCount { get; private set; }

    public static IEnumerable SimpleIntegers()
    {
        yield return 2;
        yield return 4;
        yield return 6;
    }

    public static IEnumerable<TestCaseData> ExecutableCases()
    {
        ExecutableSourceInvocationCount++;
        AcceptanceRunContext.AppendToken("TestData.ExecutableCases");
        yield return new TestCaseData("alpha").SetName("ExecutableCases_alpha");
        yield return new TestCaseData("beta").SetName("ExecutableCases_beta");
    }

    public static IEnumerable FixtureArguments()
    {
        yield return new TestFixtureData(3);
        yield return new TestFixtureData("fixture-source");
    }
}
