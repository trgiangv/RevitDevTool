using DevTools.AssemblyIsolation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using ModelContextProtocol.Protocol;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.Loader;
using System.Text;

namespace DevTools.Daemon.Mcp.Code;

/// <summary>
/// Compiles a <c>code_mode</c> method body with <see cref="CSharpCompilation"/>.
/// No metadata resolver and no source resolver: <c>#r</c> and <c>#load</c> are syntax errors.
/// </summary>
public static class CodeModeCompiler
{
    private static readonly string[] BclAssemblyNames =
    [
        "System.Private.CoreLib",
        "System.Runtime",
        "System.Linq",
        "System.Collections",
        "System.ObjectModel",
        "System.Text.Json",
        "System.Memory",
        "System.Threading",
        "netstandard",
    ];

    private static readonly AssemblyIsolationPlan IsolationPlan =
        AssemblyIsolationPlan.Create(Path.Combine(Path.GetTempPath(), "DevTools.Daemon", "DevTools.Daemon.CodeMode.dll"))
            .WithKind(AssemblyIsolationKind.Collectible);

    public static CompiledScript Compile(string? code) => Load(Emit(code));

    public static byte[] Emit(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("code is empty.", nameof(code));

        var tree = CSharpSyntaxTree.ParseText(SourceText.From(Wrap(code), Encoding.UTF8));
        var compilation = CSharpCompilation.Create(
            "DevTools.Daemon.CodeMode." + Guid.NewGuid().ToString("N"),
            [tree],
            References(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                allowUnsafe: false));

        using var pe = new MemoryStream();
        var emit = compilation.Emit(pe);
        if (!emit.Success)
        {
            var errors = string.Join(
                Environment.NewLine,
                emit.Diagnostics
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                    .Select(diagnostic => diagnostic.ToString()));
            throw new InvalidOperationException(errors);
        }

        return pe.ToArray();
    }

    public static CompiledScript Load(byte[] pe)
    {
        var session = AssemblyIsolationSession.Create(IsolationPlan);
        try
        {
            var assembly = session.LoadAssembly(pe);
            var type = assembly.GetType("DevTools.Daemon.Mcp.Code.Generated.Script")
                ?? throw new InvalidOperationException("Compiled script type was not emitted.");
            return new CompiledScript(session, type);
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    private static string Wrap(string code) =>
        """
        using System.Collections.Generic;
        using System.Linq;
        using System.Text.Json;
        using System.Threading.Tasks;
        using ModelContextProtocol.Protocol;

        namespace DevTools.Daemon.Mcp.Code.Generated;

        public sealed class Script : global::DevTools.Daemon.Mcp.Code.CatalogScript
        {
            public override async Task<object?> RunAsync()
            {
        """ + code + """

            }
        }
        """;

    private static IEnumerable<MetadataReference> References()
    {
        var trusted = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in trusted)
            byName.TryAdd(Path.GetFileNameWithoutExtension(path), path);

        foreach (var name in BclAssemblyNames)
        {
            if (byName.TryGetValue(name, out var path))
                yield return MetadataReference.CreateFromFile(path);
        }

        yield return ReferenceFrom(typeof(CatalogScript).Assembly);
        yield return ReferenceFrom(typeof(CallToolResult).Assembly);
    }

    private static unsafe MetadataReference ReferenceFrom(Assembly assembly)
    {
        if (!assembly.TryGetRawMetadata(out var blob, out var length))
            throw new InvalidOperationException($"No metadata for {assembly.GetName().Name}.");

        var module = ModuleMetadata.CreateFromMetadata((IntPtr)blob, length);
        return AssemblyMetadata.Create(module).GetReference();
    }
}

/// <summary>A collectible compiled script. Dispose drops the load context so it can unload.</summary>
public sealed class CompiledScript : IDisposable
{
    private AssemblyIsolationSession? _session;
    private Type? _type;

    internal CompiledScript(AssemblyIsolationSession session, Type type)
    {
        _session = session;
        _type = type;
    }

    internal AssemblyLoadContext LoadContext =>
        AssemblyLoadContext.GetLoadContext(
            (_type ?? throw new ObjectDisposedException(nameof(CompiledScript))).Assembly)!;

    public Task<object?> RunAsync(CancellationToken cancellationToken = default) =>
        RunAsync(runtime: null, cancellationToken);

    public async Task<object?> RunAsync(CatalogRuntime? runtime, CancellationToken cancellationToken = default)
    {
        var type = _type ?? throw new ObjectDisposedException(nameof(CompiledScript));
        var script = (CatalogScript)Activator.CreateInstance(type)!;
        if (runtime is not null)
            script.Bind(runtime, cancellationToken);
        else
            script.CancellationToken = cancellationToken;
        return await script.RunAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        var session = Interlocked.Exchange(ref _session, null);
        _type = null;
        session?.Dispose();
    }
}
