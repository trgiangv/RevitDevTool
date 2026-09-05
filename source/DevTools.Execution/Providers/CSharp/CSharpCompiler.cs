using System.IO;
using System.Reflection;
using System.Text;
using DevTools.AssemblyIsolation;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.FSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Execution.Providers.CSharp;

/// <summary>
/// Compiles a .csx script graph via Roslyn. Recursively resolves #load directives,
/// merges all #r references (NuGet, file, host-rewrite) from the entire graph,
/// collects AppDomain references, emits in-memory assembly, and finds IExternalCommand.
/// Loads compiled output through ScriptIsolationPlan: collectible ALC on modern TFMs,
/// scoped AssemblyResolve on net48. Host API assemblies are parent-bound, not isolated.
/// </summary>
public sealed class CSharpCompiler(ILogger<CSharpCompiler> logger, NugetManager nugetManager)
{
    private static readonly LanguageVersion MaxLanguageVersion =
        Enum.GetValues<LanguageVersion>()
            .Where(v => v != LanguageVersion.LatestMajor && v != LanguageVersion.Latest && v != LanguageVersion.Preview && v != LanguageVersion.Default)
            .Max();

    /// <summary>
    /// Compiles a .csx script from a file path or inline code string.
    /// If <paramref name="scriptPathOrCode"/> is an existing file, compiles directly.
    /// Otherwise treats it as inline code: writes a temp file for the directive parser.
    /// </summary>
    public async Task<ScriptCompilationResult> CompileAsync(
        string scriptPathOrCode,
        ICompiledScriptBridge hostSupport,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        string actualPath;
        var isTemp = false;

        if (File.Exists(scriptPathOrCode))
        {
            actualPath = scriptPathOrCode;
        }
        else
        {
            actualPath = Path.Combine(Path.GetTempPath(), $"mcp_{Guid.NewGuid():N}_script.csx");
            await File.WriteAllTextAsync(actualPath, scriptPathOrCode, ct).ConfigureAwait(false);
            isTemp = true;
        }

        try
        {
            var scriptName = Path.GetFileName(actualPath);
            progress?.Report($"Resolving script graph for {scriptName}...");

            var graph = CSharpDirectiveParser.ResolveGraph(actualPath, hostSupport.RewriteHostReference);

            var (allReferences, nugetDllPaths) = await ResolveReferencesAsync(graph, progress, ct).ConfigureAwait(false);

            ReportCompileProgress(progress, scriptName, graph.SourceFiles.Count);
            var peBytes = Compile(graph.SourceFiles, allReferences, out var pdbBytes, out var diagnostics);

            if (peBytes == null)
                return ScriptCompilationResult.Failed(diagnostics);

            return LoadAndCreateCommand(peBytes, pdbBytes, nugetDllPaths, hostSupport);
        }
        finally
        {
            if (isTemp)
            {
                try { File.Delete(actualPath); }
                catch { /* best-effort cleanup */ }
            }
        }
    }

    private ScriptCompilationResult LoadAndCreateCommand(
        byte[] peBytes, byte[]? pdbBytes, IReadOnlyList<string> nugetDllPaths, ICompiledScriptBridge hostSupport)
    {
        var session = AssemblyIsolationSession.Create(
            ScriptIsolationPlan.Create(
                $"CsxScript_{Guid.NewGuid():N}",
                nugetDllPaths,
                hostSupport.GetParentBindings(),
                new ScriptIsolationDiagnosticSink(logger)));
        try
        {
            var assembly = session.LoadAssembly(peBytes, pdbBytes);
            return CreateCommandResult(assembly, hostSupport, session);
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    private async Task<(HashSet<string> AllReferences, IReadOnlyList<string> NugetDlls)> ResolveReferencesAsync(
        ScriptGraph graph, IProgress<string>? progress, CancellationToken ct)
    {
        var references = new HashSet<string>(graph.AssemblyReferences, StringComparer.OrdinalIgnoreCase);
        var nugetDlls = new List<string>();

        if (graph.Packages.Count > 0)
        {
            progress?.Report($"Resolving {graph.Packages.Count} NuGet package(s)...");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pkg in graph.Packages)
            {
                if (!seen.Add(pkg.PackageId))
                    continue;
                var dlls = await nugetManager.ResolvePackageDllsAsync(pkg.PackageId, pkg.Version, ct).ConfigureAwait(false);
                foreach (var dll in dlls)
                {
                    references.Add(dll);
                    nugetDlls.Add(dll);
                }
            }
        }

        CollectAppDomainReferences(references);
        return (references, nugetDlls);
    }

    private static ScriptCompilationResult CreateCommandResult(Assembly assembly, ICompiledScriptBridge hostSupport, IDisposable? cleanup)
    {
        var commandType = hostSupport.TryFindCommandType(assembly);
        if (commandType == null)
        {
            cleanup?.Dispose();
            return ScriptCompilationResult.Failed("No host command type found in compiled script.");
        }

        var instance = Activator.CreateInstance(commandType);
        if (instance == null)
        {
            cleanup?.Dispose();
            return ScriptCompilationResult.Failed($"Failed to create instance of {commandType.FullName}.");
        }

        return ScriptCompilationResult.Succeeded(instance, cleanup);
    }

    private static void ReportCompileProgress(IProgress<string>? progress, string scriptName, int fileCount)
    {
        progress?.Report(fileCount > 1
            ? $"Compiling {scriptName} + {fileCount - 1} loaded file(s)..."
            : $"Compiling {scriptName}...");
    }

    private byte[]? Compile(
        IReadOnlyList<SourceFileEntry> sourceFiles,
        HashSet<string> referencePaths,
        out byte[]? pdbBytes,
        out List<string> diagnostics)
    {
        diagnostics = [];
        pdbBytes = null;

        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(MaxLanguageVersion)
            .WithKind(SourceCodeKind.Regular)
            .WithPreprocessorSymbols("TRACE", "DEBUG");

        var syntaxTrees = sourceFiles
            .Select(f =>
            {
                var text = SourceText.From(f.CleanSource, Encoding.UTF8);
                return CSharpSyntaxTree.ParseText(text, parseOptions, f.Path);
            })
            .ToList();

        var metadataRefs = LoadMetadataReferences(referencePaths);

        var compilation = CSharpCompilation.Create(
            assemblyName: $"CsxScript_{Guid.NewGuid():N}",
            syntaxTrees: syntaxTrees,
            references: metadataRefs,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOverflowChecks(true)
                .WithPlatform(Platform.X64)
                .WithOptimizationLevel(OptimizationLevel.Debug));

        var embeddedTexts = syntaxTrees
            .Select(tree => EmbeddedText.FromSource(tree.FilePath, tree.GetText()))
            .ToList();

        using var peStream = new MemoryStream();
        using var pdbStream = new MemoryStream();
        var emitResult = compilation.Emit(
            peStream,
            pdbStream,
            options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb),
            embeddedTexts: embeddedTexts);

        diagnostics.AddRange(emitResult.Diagnostics
            .Where(d => d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .Select(d => d.ToString()));

        if (!emitResult.Success)
            return null;

        pdbBytes = pdbStream.ToArray();
        return peStream.ToArray();
    }

    private List<MetadataReference> LoadMetadataReferences(HashSet<string> referencePaths)
    {
        var refs = new List<MetadataReference>();
        foreach (var refPath in referencePaths)
        {
            if (!File.Exists(refPath))
            {
                logger.ZLogDebug($"[CSharpCompiler] Skipping missing reference: {refPath}");
                continue;
            }

            try
            {
                refs.Add(MetadataReference.CreateFromFile(refPath));
            }
            catch (Exception ex)
            {
                logger.ZLogDebug($"[CSharpCompiler] Failed to load reference '{refPath}': {ex.Message}");
            }
        }
        return refs;
    }

    private static void CollectAppDomainReferences(HashSet<string> references)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
                continue;

            try
            {
                var location = assembly.Location;
                if (!string.IsNullOrEmpty(location) && File.Exists(location))
                    references.Add(location);
            }
            catch
            {
                // ignored
            }
        }
    }

    private sealed class ScriptIsolationDiagnosticSink(ILogger logger) : IAssemblyIsolationDiagnosticSink
    {
        public void Publish(AssemblyIsolationDiagnostic diagnostic) => logger.ZLogDebug(
            $"[CSharpCompiler] Assembly isolation diagnostic '{diagnostic.Code}': {diagnostic.Message}");
    }
}
