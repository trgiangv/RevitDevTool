using DevTools.AssemblyIsolation;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Sources;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Transport;
using Microsoft.Extensions.Logging;

namespace DevTools.Testing.Host.Runtime;

/// <summary>
/// Activates the framework runtime named by the manifest in the user's output, on the
/// user's closure. Bind failures during activation or a run become the one-sentence stop.
/// </summary>
internal sealed class OutputSessionFactory(TestFrameworkId frameworkId, ILogger logger) : ITestingRuntimeSessionFactory
{
    public ITestingRuntimeSession Create(TestingGenerationManifest generation)
    {
        ArgumentNullException.ThrowIfNull(generation);
        var root = Path.GetFullPath(generation.ShadowDirectory);
        var manifest = RuntimeCompatChecker.Check(frameworkId, root);
        var found = RuntimeCompatChecker.ReadFrameworkVersion(manifest, root);
        var adapterVersion = RuntimeManifest.ReadAdapterVersion(root);

        var plan = AssemblyIsolationPlan.Create(generation.RuntimeAssemblyPath)
            .WithKind(AssemblyIsolationKind.Isolated)
#if NETFRAMEWORK
            .WithDistinctFileIdentity()
#endif
            .Pin(typeof(ITestingRuntimeSession).Assembly)
            .WithDiagnosticSink(new LoggerDiagnosticSink(logger))
            .AddManagedSource(new ManifestAssemblySource(
                generation.ManagedAssemblies.Select(path => new AssemblyCandidate(path, root)),
                bindsAnyVersion: true));

        return IsolatedRuntimeActivator.Activate(
            generation,
            plan,
            manifest.Entry,
            testAssembly => [testAssembly, generation.ShadowAssemblyPath, generation.GenerationId],
            failure =>
            {
                var sentence = RuntimeBindFailure.TryExplain(failure, manifest, found, adapterVersion);
                if (sentence is not null)
                    logger.LogWarning(failure, "{Framework} runtime bind failure: {Sentence}", manifest.Framework, sentence);

                return sentence;
            });
    }

    private sealed class LoggerDiagnosticSink(ILogger logger) : IAssemblyIsolationDiagnosticSink
    {
        public void Publish(AssemblyIsolationDiagnostic diagnostic) =>
            logger.Log(
                diagnostic.Code.EndsWith("-rejected", StringComparison.Ordinal) ? LogLevel.Warning : LogLevel.Debug,
                "Testing assembly isolation '{Code}': {Message}",
                diagnostic.Code,
                diagnostic.Message);
    }
}
