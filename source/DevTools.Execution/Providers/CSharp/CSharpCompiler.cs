using System.Diagnostics;
using System.IO;
using System.Reflection;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.FSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DevTools.Execution.Providers.CSharp;

/// <summary>
/// Compiles a .csx script graph via Roslyn. Recursively resolves #load directives,
/// merges all #r references (NuGet, file, host-rewrite) from the entire graph,
/// collects AppDomain references, emits in-memory assembly, and finds IExternalCommand.
/// </summary>
internal static class CSharpCompiler
{
    private static readonly LanguageVersion MaxLanguageVersion =
        Enum.GetValues<LanguageVersion>()
            .Where(v => v != LanguageVersion.LatestMajor && v != LanguageVersion.Latest && v != LanguageVersion.Preview && v != LanguageVersion.Default)
            .Max();

    public static async Task<ScriptCompilationResult> CompileAsync(
        string scriptPath,
        ICompiledScriptBridge hostSupport,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var scriptName = Path.GetFileName(scriptPath);
        progress?.Report($"Resolving script graph for {scriptName}...");

        var graph = CSharpDirectiveParser.ResolveGraph(
            scriptPath, hostSupport.GetHostReferencePattern(), hostSupport.GetHostReferenceReplacement());

        var references = await ResolveReferencesAsync(graph, progress, ct).ConfigureAwait(false);

        ReportCompileProgress(progress, scriptName, graph.SourceFiles.Count);
        var assembly = Compile(graph.SourceFiles, references, out var diagnostics);

        if (assembly == null)
            return ScriptCompilationResult.Failed(diagnostics);

        return CreateCommandResult(assembly, hostSupport);
    }

    private static async Task<HashSet<string>> ResolveReferencesAsync(
        ScriptGraph graph, IProgress<string>? progress, CancellationToken ct)
    {
        var references = new HashSet<string>(graph.AssemblyReferences, StringComparer.OrdinalIgnoreCase);

        if (graph.Packages.Count > 0)
        {
            progress?.Report($"Resolving {graph.Packages.Count} NuGet package(s)...");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pkg in graph.Packages)
            {
                if (!seen.Add(pkg.PackageId))
                    continue;
                var dlls = await NugetManager.ResolvePackageDllsAsync(pkg.PackageId, pkg.Version, ct).ConfigureAwait(false);
                foreach (var dll in dlls)
                    references.Add(dll);
            }
        }

        CollectAppDomainReferences(references);
        return references;
    }

    private static ScriptCompilationResult CreateCommandResult(Assembly assembly, ICompiledScriptBridge hostSupport)
    {
        var commandType = hostSupport.TryFindCommandType(assembly);
        if (commandType == null)
            return ScriptCompilationResult.Failed("No type implementing IExternalCommand found in compiled script.");

        var instance = Activator.CreateInstance(commandType);
        if (instance == null)
            return ScriptCompilationResult.Failed($"Failed to create instance of {commandType.FullName}.");

        return ScriptCompilationResult.Succeeded(instance);
    }

    private static void ReportCompileProgress(IProgress<string>? progress, string scriptName, int fileCount)
    {
        progress?.Report(fileCount > 1 
            ? $"Compiling {scriptName} + {fileCount - 1} loaded file(s)..."
            : $"Compiling {scriptName}...");
    }

    private static Assembly? Compile(IReadOnlyList<SourceFileEntry> sourceFiles, HashSet<string> referencePaths, out List<string> diagnostics)
    {
        diagnostics = [];

        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(MaxLanguageVersion)
            .WithKind(SourceCodeKind.Regular);

        var syntaxTrees = sourceFiles
            .Select(f => CSharpSyntaxTree.ParseText(f.CleanSource, parseOptions, f.Path))
            .ToList();

        var metadataRefs = LoadMetadataReferences(referencePaths);

        var compilation = CSharpCompilation.Create(
            assemblyName: $"CsxScript_{Guid.NewGuid():N}",
            syntaxTrees: syntaxTrees,
            references: metadataRefs,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithOverflowChecks(true)
                .WithPlatform(Platform.X64)
                .WithOptimizationLevel(OptimizationLevel.Release));

        using var peStream = new MemoryStream();
        var emitResult = compilation.Emit(peStream);

        diagnostics.AddRange(emitResult.Diagnostics
            .Where(d => d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning)
            .Select(d => d.ToString()));

        if (!emitResult.Success)
            return null;

        peStream.Seek(0, SeekOrigin.Begin);
        return Assembly.Load(peStream.ToArray());
    }

    private static List<MetadataReference> LoadMetadataReferences(HashSet<string> referencePaths)
    {
        var refs = new List<MetadataReference>();
        foreach (var refPath in referencePaths)
        {
            if (!File.Exists(refPath))
            {
                Debug.WriteLine($"[CSharpCompiler] Skipping missing reference: {refPath}");
                continue;
            }

            try
            {
                refs.Add(MetadataReference.CreateFromFile(refPath));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CSharpCompiler] Failed to load reference '{refPath}': {ex.Message}");
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
                // Some assemblies may not have a location
            }
        }
    }
}