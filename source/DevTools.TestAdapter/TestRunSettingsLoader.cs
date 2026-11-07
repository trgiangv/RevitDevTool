using DevTools.Testing.Abstractions.Config;
using DevTools.Testing.Abstractions.Contracts;
using Microsoft.Testing.Platform.Configurations;
using System.Reflection;

namespace DevTools.TestAdapter;

/// <summary>
/// Reads the host options the test csproj declared. Order per key: environment
/// variable, the <c>devtools</c> section of a user-authored testconfig.json through MTP
/// <see cref="IConfiguration"/>, then the <c>devtools.*</c> assembly metadata the package
/// targets write into the testhost. The runner path comes from that metadata only.
/// </summary>
internal static class TestRunSettingsLoader
{
    private const string MetadataPrefix = TestConfig.SectionName + ".";

    private const string MissingConfigMessage =
        "RevitDevTool.TestAdapter requires HostName and HostVersion. Declare them in the test .csproj, or author a '"
        + TestConfig.SectionName + "' section in " + TestConfig.FileName + " beside the .csproj.";

    internal static TestRunSettings Load(IConfiguration configuration, Assembly? testAssembly = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var metadata = ReadMetadata(testAssembly);

        var hostName = TestingRunnerPaths.ReadEnvironment(TestingRunnerPaths.HostEnvironmentVariable)
            ?? ReadKey(configuration, metadata, TestConfig.Keys.HostName);
        var hostVersion = TestingRunnerPaths.ReadEnvironment(TestingRunnerPaths.HostVersionEnvironmentVariable)
            ?? ReadKey(configuration, metadata, TestConfig.Keys.HostVersion);
        if (hostName is null || hostVersion is null
            || string.IsNullOrWhiteSpace(hostName) || string.IsNullOrWhiteSpace(hostVersion))
            throw new InvalidOperationException(MissingConfigMessage);

        if (!int.TryParse(ReadKey(configuration, metadata, TestConfig.Keys.PerTestTimeoutSeconds), out var perTestTimeout)
            || perTestTimeout <= 0)
            throw new InvalidOperationException(MissingConfigMessage);
        if (!int.TryParse(ReadKey(configuration, metadata, TestConfig.Keys.LaunchTimeoutSeconds), out var launchTimeout)
            || launchTimeout <= 0)
            throw new InvalidOperationException(MissingConfigMessage);

        bool.TryParse(ReadKey(configuration, metadata, TestConfig.Keys.ForceLaunch), out var forceLaunch);

        var frameworkId = ReadKey(configuration, metadata, TestConfig.Keys.FrameworkId);
        if (string.IsNullOrWhiteSpace(frameworkId)
            || !Enum.TryParse(frameworkId, ignoreCase: true, out TestFrameworkId parsed)
            || !Enum.IsDefined(parsed))
        {
            throw new InvalidOperationException(
                "RevitDevTool.TestAdapter requires 'devtools.frameworkId' to be "
                + nameof(TestFrameworkId.NUnit) + ", " + nameof(TestFrameworkId.TUnit) + ", or " + nameof(TestFrameworkId.MSTest)
                + ". Set TestingFramework in the test .csproj.");
        }
        metadata.TryGetValue(TestConfig.Keys.RunnerPath, out var runnerPath);

        return new TestRunSettings(
            new TestHostOptions(
                hostName.Trim(),
                hostVersion.Trim(),
                forceLaunch,
                perTestTimeout,
                launchTimeout),
            parsed,
            TestingRunnerPaths.ExpandPath(runnerPath));
    }

    internal static IReadOnlyDictionary<string, string> ReadMetadata(Assembly? assembly)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (assembly is null)
            return values;

        foreach (var attribute in assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (attribute.Key is not null
                && attribute.Key.StartsWith(MetadataPrefix, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(attribute.Value))
            {
                values[attribute.Key[MetadataPrefix.Length..]] = attribute.Value.Trim();
            }
        }

        return values;
    }

    private static string? ReadKey(IConfiguration configuration, IReadOnlyDictionary<string, string> metadata, string name)
    {
        var value = configuration[TestConfig.Keys.Configuration(name)];
        if (value is not null && !string.IsNullOrWhiteSpace(value))
            return value.Trim();

        return metadata.TryGetValue(name, out var fromMetadata) ? fromMetadata : null;
    }
}
