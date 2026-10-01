using System.Reflection;
using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Contracts;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace DevTools.MSTest.MTP;

/// <summary>
/// One <c>--list-tests</c> pass. Both the testhost discoverer and the in-host runtime
/// use it, so a filtered run and the tree that published it share the same uids.
/// </summary>
internal static class MSTestListing
{
    public static List<TestDiscoveredTest> List(Assembly testAssembly)
    {
        ArgumentNullException.ThrowIfNull(testAssembly);
        var resultsDirectory = Path.Combine(Path.GetTempPath(), "devtools-mstest-list-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(resultsDirectory);
        ITestApplication? application = null;
        // --list-tests does not attach TestHost data consumers. The platform output
        // device still buffers the discovery nodes and writes them as JSON to the
        // process stdout stream, which Console.SetOut does not own.
        using var stdout = MtpDiscoveryInternals.ConsoleSilence.Begin();
        try
        {
            var builder = TestApplication.CreateBuilderAsync(
                    ["--list-tests", "json", "--results-directory", resultsDirectory],
                    new TestApplicationOptions { EnableTelemetry = false })
                .GetAwaiter()
                .GetResult();
            builder.AddMSTest(() => new[] { testAssembly });
            application = builder.BuildAsync().GetAwaiter().GetResult();
            var exitCode = application.RunAsync().GetAwaiter().GetResult();
            var discovered = MapDiscovered(MtpDiscoveryInternals.ReadDiscoveredNodes(application));
            if (exitCode != 0 && discovered.Count == 0)
            {
                throw new InvalidOperationException(
                    "MSTest TestApplication.RunAsync returned " + exitCode + " without discovered tests.");
            }

            return discovered;
        }
        finally
        {
            if (application is IDisposable disposable)
                disposable.Dispose();
            TryDeleteDirectory(resultsDirectory);
        }
    }

    private static List<TestDiscoveredTest> MapDiscovered(IReadOnlyList<TestNode> nodes)
    {
        var discovered = new List<TestDiscoveredTest>();
        var unexpected = new List<string>();
        foreach (var node in nodes)
        {
            var state = node.Properties.SingleOrDefault<TestNodeStateProperty>();
            if (state is not DiscoveredTestNodeStateProperty)
            {
                if (state is not null)
                    unexpected.Add(state.GetType().Name);
                continue;
            }

            var identifier = node.Properties.SingleOrDefault<TestMethodIdentifierProperty>();
            if (identifier is null)
            {
                throw new TestingDiscoveryFailedException(
                    "MSTest --list-tests returned a discovered test without TestMethodIdentifierProperty.");
            }

            discovered.Add(ToDiscovered(node, identifier));
        }

        if (discovered.Count == 0 && unexpected.Count > 0)
        {
            throw new InvalidOperationException(
                "MSTest --list-tests published execution results instead of discovery nodes: "
                + string.Join(", ", unexpected.Distinct(StringComparer.Ordinal)));
        }

        return discovered;
    }

    private static TestDiscoveredTest ToDiscovered(TestNode node, TestMethodIdentifierProperty identifier)
    {
        var typeName = identifier.TypeName;
        var namespaceName = identifier.Namespace;
        var className = string.IsNullOrEmpty(namespaceName) ? typeName : namespaceName + "." + typeName;
        var fullyQualifiedName = className + "." + identifier.MethodName;
        var location = node.Properties.SingleOrDefault<TestFileLocationProperty>();
        var categories = new List<string>();
        foreach (var metadata in node.Properties.OfType<TestMetadataProperty>())
        {
            if (!string.IsNullOrEmpty(metadata.Key) && string.IsNullOrEmpty(metadata.Value))
                categories.Add(metadata.Key);
        }

        return new TestDiscoveredTest(
            node.Uid.Value,
            node.DisplayName,
            fullyQualifiedName,
            className,
            identifier.MethodName,
            location is null
                ? null
                : new TestSourceLocation(location.FilePath, location.LineSpan.Start.Line),
            string.IsNullOrEmpty(namespaceName) ? null : namespaceName,
            typeName,
            MethodArity: identifier.MethodArity,
            Categories: categories.Count == 0 ? null : categories,
            ParameterTypeFullNames: identifier.ParameterTypeFullNames.Length == 0
                ? null
                : identifier.ParameterTypeFullNames,
            ReturnTypeFullName: string.IsNullOrEmpty(identifier.ReturnTypeFullName)
                ? null
                : identifier.ReturnTypeFullName);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
