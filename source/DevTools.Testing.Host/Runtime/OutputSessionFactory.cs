using DevTools.AssemblyIsolation;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Sources;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Transport;
using Microsoft.Extensions.Logging;
using System.Reflection;
using ZLogger;

namespace DevTools.Testing.Host.Runtime;

/// <summary>
/// Activates the framework runtime named by the manifest in the user's output, on the
/// user's closure. Bind failures during activation or a run become the one-sentence stop.
/// With <paramref name="shareFramework"/>, the framework is one host-wide copy shared with the
/// session, and the session runs on the calling thread.
/// </summary>
internal sealed class OutputSessionFactory(
    TestFrameworkId frameworkId,
    ILogger logger,
    Func<string, Assembly>? shareFramework = null) : ITestingRuntimeSessionFactory
{
    public ITestingRuntimeSession Create(TestingGenerationManifest generation)
    {
        ArgumentNullException.ThrowIfNull(generation);
        var root = Path.GetFullPath(generation.ShadowDirectory);
        var manifest = RuntimeCompatChecker.Check(frameworkId, root);
        var found = RuntimeCompatChecker.ReadFrameworkVersion(manifest, root);
        var adapterVersion = RuntimeManifest.ReadAdapterVersion(root);
        var shared = shareFramework?.Invoke(Path.Combine(root, manifest.Assembly + ".dll"));

        return IsolatedRuntimeActivator.Activate(
            generation,
            CreatePlan(generation, shared, logger),
            manifest.Entry,
            testAssembly => shared is null
                ? [testAssembly, generation.ShadowAssemblyPath, generation.GenerationId]
                : [testAssembly, generation.ShadowAssemblyPath, generation.GenerationId, true],
            failure =>
            {
                var sentence = RuntimeBindFailure.TryExplain(failure, manifest, found, adapterVersion);
                if (sentence is not null)
                    logger.ZLogWarning(failure, $"{manifest.Framework} runtime bind failure: {sentence}");

                return sentence;
            });
    }

    internal static AssemblyIsolationPlan CreatePlan(
        TestingGenerationManifest generation,
        Assembly? shared,
        ILogger logger)
    {
        var plan = AssemblyIsolationPlan.Create(generation.RuntimeAssemblyPath)
            .WithKind(AssemblyIsolationKind.Isolated)
#if NETFRAMEWORK
            .WithDistinctFileIdentity()
#endif
            .Pin(typeof(ITestingRuntimeSession).Assembly)
            .WithDiagnosticSink(new LoggerDiagnosticSink(logger))
            .AddManagedSource(new ManifestAssemblySource(
                generation.ManagedAssemblies.Select(path => new AssemblyCandidate(path, Path.GetFullPath(generation.ShadowDirectory))),
                bindsAnyVersion: true));

        return shared is null ? plan : plan.ShareWithClosure(shared);
    }

    private sealed class LoggerDiagnosticSink(ILogger logger) : IAssemblyIsolationDiagnosticSink
    {
        public void Publish(AssemblyIsolationDiagnostic diagnostic)
        {
            if (diagnostic.IsRejection)
            {
                logger.ZLogWarning($"Testing assembly isolation '{diagnostic.Code}': {diagnostic.Message}");
                return;
            }
#if DEBUG
            logger.ZLogDebug($"Testing assembly isolation '{diagnostic.Code}': {diagnostic.Message}");
#endif
        }
    }
}
