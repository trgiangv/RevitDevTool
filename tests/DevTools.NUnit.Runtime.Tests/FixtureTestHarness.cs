using System.Reflection;
using DevTools.NUnit.Runtime.Fixtures;

namespace DevTools.NUnit.Runtime.Tests;

internal static class FixtureTestHarness
{
    public const string GenerationId = "runtime-acceptance";
    public const string RunId = "runtime-tests";

    public static string FixtureAssemblyPath =>
        Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "DevTools.NUnit.Runtime.Fixtures.dll"));

    public static Assembly LoadFixtureAssembly() =>
        Assembly.LoadFrom(FixtureAssemblyPath);

    public static NUnitRuntimeSession CreateSession()
    {
        Environment.SetEnvironmentVariable(AcceptanceRunContext.RunIdEnvironmentVariable, RunId);
        return new NUnitRuntimeSession(LoadFixtureAssembly(), FixtureAssemblyPath, GenerationId);
    }

    public static void ResetAcceptanceLog()
    {
        Environment.SetEnvironmentVariable(AcceptanceRunContext.RunIdEnvironmentVariable, RunId);
        if (File.Exists(AcceptanceRunContext.LogPath))
            File.Delete(AcceptanceRunContext.LogPath);
    }

    public static IReadOnlyList<string> ReadAcceptanceTokens() => AcceptanceRunContext.ReadTokens();
}
