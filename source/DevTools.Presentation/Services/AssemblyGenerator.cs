using System.IO;
using System.Reflection;

namespace DevTools.Presentation.Services;

/// <summary>
/// Writes in-memory assemblies to disk with ILPack. A no-disk-path reference is dumped
/// before the assembly that uses it. ILPack's manifest table misses some type assemblies;
/// <see cref="AssemblyPatcher"/> records that exact identity before the type reference is written.
/// </summary>
public static class AssemblyGenerator
{
    [PublicAPI]
    public readonly record struct Result(Assembly Assembly, string? OutputFile, string? Error);

    public static Result[] Generate(IReadOnlyList<Assembly> assemblies, string outputDirectory)
    {
        if (assemblies.Count == 0)
            return [];

        Directory.CreateDirectory(outputDirectory);
        AssemblyPatcher.Apply();
        var loaded = IndexByFullName(AppDomain.CurrentDomain.GetAssemblies());
        var chain = PathlessChain(assemblies, loaded);
        var generator = new Lokad.ILPack.AssemblyGenerator();
        var results = new Result[chain.Count];

        using var resolveScope = new AssemblyResolveScope(loaded);
        for (var i = 0; i < chain.Count; i++)
            results[i] = Generate(generator, chain[i], loaded, outputDirectory);

        return results;
    }

    private static Result Generate(
        Lokad.ILPack.AssemblyGenerator generator,
        Assembly assembly,
        Dictionary<string, Assembly> loaded,
        string outputDirectory)
    {
        var path = Path.Combine(outputDirectory, assembly.GetName().Name + ".dll");
        try
        {
            generator.GenerateAssembly(assembly, LoadedReferences(assembly, loaded), path);
            return new Result(assembly, path, null);
        }
        catch (Exception ex)
        {
            return new Result(assembly, null, Describe(ex));
        }
    }

    private static string Describe(Exception exception)
    {
        var error = exception.GetBaseException();
        var frame = error.StackTrace?.Split('\n')[0].Trim();
        return string.IsNullOrEmpty(frame) ? error.Message : $"{error.Message} | {frame}";
    }

    /// <summary>
    /// Pathless roots and their pathless references. Dependencies come first.
    /// Only an already loaded assembly with the same <see cref="AssemblyName.FullName"/> joins the chain.
    /// </summary>
    private static List<Assembly> PathlessChain(
        IReadOnlyList<Assembly> roots,
        Dictionary<string, Assembly> loaded)
    {
        var ordered = new List<Assembly>();
        var visited = new HashSet<Assembly>();
        foreach (var root in roots)
            Visit(root, loaded, ordered, visited);

        return ordered;
    }

    private static void Visit(
        Assembly assembly,
        Dictionary<string, Assembly> loaded,
        List<Assembly> ordered,
        HashSet<Assembly> visited)
    {
        if (assembly.IsDynamic || !visited.Add(assembly) || HasDiskPath(assembly))
            return;

        foreach (var reference in assembly.GetReferencedAssemblies())
            VisitReference(reference, loaded, ordered, visited);

        ordered.Add(assembly);
    }

    private static void VisitReference(
        AssemblyName reference,
        Dictionary<string, Assembly> loaded,
        List<Assembly> ordered,
        HashSet<Assembly> visited)
    {
        if (loaded.TryGetValue(reference.FullName, out var match))
            Visit(match, loaded, ordered, visited);
    }

    private static Assembly[] LoadedReferences(Assembly source, Dictionary<string, Assembly> loaded)
    {
        var references = new List<Assembly>();
        foreach (var reference in source.GetReferencedAssemblies())
            AddLoadedReference(source, reference, loaded, references);

        return references.ToArray();
    }

    private static void AddLoadedReference(
        Assembly source,
        AssemblyName reference,
        Dictionary<string, Assembly> loaded,
        List<Assembly> references)
    {
        if (!loaded.TryGetValue(reference.FullName, out var match))
            return;
        if (!ReferenceEquals(match, source))
            references.Add(match);
    }

    private static Dictionary<string, Assembly> IndexByFullName(Assembly[] loaded)
    {
        var index = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        foreach (var candidate in loaded)
            AddIndex(index, candidate);

        return index;
    }

    private static void AddIndex(Dictionary<string, Assembly> index, Assembly candidate)
    {
        if (candidate.IsDynamic || string.IsNullOrEmpty(candidate.FullName))
            return;
        index.TryAdd(candidate.FullName, candidate);
    }

    private static bool HasDiskPath(Assembly assembly)
    {
        if (!string.IsNullOrEmpty(assembly.Location) && File.Exists(assembly.Location))
            return true;

        return assembly.GetModules(false).Any(module => File.Exists(module.FullyQualifiedName));
    }

    /// <summary>
    /// ILPack calls <c>Assembly.Load</c> for a manifest reference that has no file.
    /// Return the loaded instance only when the identity matches. A different version is not a substitute.
    /// </summary>
    private sealed class AssemblyResolveScope : IDisposable
    {
        private readonly Dictionary<string, Assembly> _byFullName;

        public AssemblyResolveScope(Dictionary<string, Assembly> byFullName)
        {
            _byFullName = byFullName;
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        }

        public void Dispose() => AppDomain.CurrentDomain.AssemblyResolve -= Resolve;

        private Assembly? Resolve(object? sender, ResolveEventArgs args)
        {
            var fullName = new AssemblyName(args.Name).FullName;
            return _byFullName.GetValueOrDefault(fullName);

        }
    }
}
