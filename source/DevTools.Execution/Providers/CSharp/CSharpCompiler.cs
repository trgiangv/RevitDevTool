using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers.FSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DevTools.Execution.Providers.CSharp;

/// <summary>
/// Compiles a .csx script via Roslyn. Resolves #r directives (NuGet, file, host-rewrite),
/// collects AppDomain references, emits in-memory assembly, and finds IExternalCommand.
/// </summary>
internal static class CSharpCompiler
{
    private static readonly LanguageVersion MaxLanguageVersion =
        Enum.GetValues(typeof(LanguageVersion))
            .Cast<LanguageVersion>()
            .Where(v => v != LanguageVersion.LatestMajor && v != LanguageVersion.Latest && v != LanguageVersion.Preview && v != LanguageVersion.Default)
            .Max();

    public static async Task<CSharpCompilationResult> CompileAsync(
        string scriptPath,
        IFSharpHostSupport hostSupport,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var scriptName = Path.GetFileName(scriptPath);
        progress?.Report($"Parsing directives for {scriptName}...");

        var source = await ReadFileAsync(scriptPath, ct).ConfigureAwait(false);
        var hostPattern = hostSupport.GetHostReferencePattern();
        var hostReplacement = hostSupport.GetHostReferenceReplacement();

        var parsed = CSharpDirectiveParser.Parse(source, hostPattern, hostReplacement);

        var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var fileRef in parsed.FileReferences)
            references.Add(fileRef);

        if (parsed.Packages.Count > 0)
        {
            progress?.Report($"Resolving {parsed.Packages.Count} NuGet package(s)...");
            foreach (var pkg in parsed.Packages)
            {
                var dlls = await NugetManager.ResolvePackageDllsAsync(pkg.PackageId, pkg.Version, ct).ConfigureAwait(false);
                foreach (var dll in dlls)
                    references.Add(dll);
            }
        }

        CollectAppDomainReferences(references);

        progress?.Report($"Compiling {scriptName}...");
        var assembly = Compile(parsed.CleanSource, scriptPath, references, out var diagnostics);

        if (assembly == null)
            return CSharpCompilationResult.Failed(diagnostics);

        var commandType = hostSupport.TryFindCommandType(assembly);
        if (commandType == null)
            return CSharpCompilationResult.Failed(["No type implementing IExternalCommand found in compiled script."]);

        var instance = Activator.CreateInstance(commandType);
        if (instance == null)
            return CSharpCompilationResult.Failed([$"Failed to create instance of {commandType.FullName}."]);

        return CSharpCompilationResult.Succeeded(instance);
    }

    private static Assembly? Compile(string source, string filePath, HashSet<string> referencePaths, out List<string> diagnostics)
    {
        diagnostics = [];

        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(MaxLanguageVersion)
            .WithKind(SourceCodeKind.Regular);

        var syntaxTree = CSharpSyntaxTree.ParseText(
            text: source,
            options: parseOptions,
            path: filePath);

        var metadataRefs = new List<MetadataReference>();
        foreach (var refPath in referencePaths)
        {
            if (!File.Exists(refPath))
            {
                Debug.WriteLine($"[CSharpCompiler] Skipping missing reference: {refPath}");
                continue;
            }

            try
            {
                metadataRefs.Add(MetadataReference.CreateFromFile(refPath));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CSharpCompiler] Failed to load reference '{refPath}': {ex.Message}");
            }
        }

        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithOverflowChecks(true)
            .WithPlatform(Platform.X64)
            .WithOptimizationLevel(OptimizationLevel.Release);

        var compilation = CSharpCompilation.Create(
            assemblyName: $"CsxScript_{Guid.NewGuid():N}",
            syntaxTrees: [syntaxTree],
            references: metadataRefs,
            options: compilationOptions);

        using var peStream = new MemoryStream();
        var emitResult = compilation.Emit(peStream);

        foreach (var diag in emitResult.Diagnostics)
        {
            if (diag.Severity == DiagnosticSeverity.Error || diag.Severity == DiagnosticSeverity.Warning)
                diagnostics.Add(diag.ToString());
        }

        if (!emitResult.Success)
            return null;

        peStream.Seek(0, SeekOrigin.Begin);
        return Assembly.Load(peStream.ToArray());
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

#if NET
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        AddIfExists(references, Path.Combine(runtimeDir, "mscorlib.dll"));
        AddIfExists(references, Path.Combine(runtimeDir, "netstandard.dll"));
        AddIfExists(references, Path.Combine(runtimeDir, "System.Runtime.dll"));
#endif
    }

    private static void AddIfExists(HashSet<string> references, string path)
    {
        if (File.Exists(path))
            references.Add(path);
    }

    private static async Task<string> ReadFileAsync(string path, CancellationToken ct)
    {
#if NET
        return await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
#else
        using var reader = new StreamReader(path);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
#endif
    }
}

internal sealed class CSharpCompilationResult
{
    public bool Success { get; private init; }
    public object? Command { get; private init; }
    public IReadOnlyList<string> Diagnostics { get; private init; } = [];

    public static CSharpCompilationResult Succeeded(object command) =>
        new() { Success = true, Command = command };

    public static CSharpCompilationResult Failed(IReadOnlyList<string> diagnostics) =>
        new() { Success = false, Diagnostics = diagnostics };

    public static CSharpCompilationResult Failed(List<string> diagnostics) =>
        new() { Success = false, Diagnostics = diagnostics };
}
