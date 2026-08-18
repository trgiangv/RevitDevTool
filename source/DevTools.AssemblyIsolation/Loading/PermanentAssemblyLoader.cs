using System.Reflection;
using System.Security.Cryptography;
using DevTools.AssemblyIsolation.Diagnostics;
#if NET
using System.Runtime.Loader;
#endif

namespace DevTools.AssemblyIsolation.Loading;

public sealed class PermanentAssemblyLoader
{
    readonly object gate = new();
    readonly Dictionary<string, LoadedPath> assembliesByPath = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, SelectedAssembly> assembliesByIdentity = new(StringComparer.OrdinalIgnoreCase);
    readonly IAssemblyIsolationDiagnosticSink? diagnostics;
#if NET
    readonly AssemblyLoadContext loadContext = new("DevToolsPermanent", isCollectible: false);

    internal AssemblyLoadContext LoadContext => loadContext;
#endif

    public PermanentAssemblyLoader(IAssemblyIsolationDiagnosticSink? diagnostics = null) => this.diagnostics = diagnostics;

    public Assembly LoadPath(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
            throw new ArgumentException("An assembly path is required.", nameof(assemblyPath));

        var normalizedPath = Path.GetFullPath(assemblyPath);
        var requestedIdentity = AssemblyName.GetAssemblyName(normalizedPath);
        var identity = requestedIdentity.FullName
            ?? throw new InvalidOperationException($"Assembly '{normalizedPath}' has no full identity.");
        var fingerprint = ComputeFingerprint(normalizedPath);

        lock (gate)
        {
            if (assembliesByPath.TryGetValue(normalizedPath, out var pathMatch))
            {
                if (!string.Equals(pathMatch.Identity, identity, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(pathMatch.Fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    Publish("permanent-path-changed", requestedIdentity,
                        $"Path '{normalizedPath}' changed after '{pathMatch.Identity}' was loaded. Returning the initial assembly; permanent loading does not support hot reload.");
                }

                return pathMatch.Assembly;
            }

            if (assembliesByIdentity.TryGetValue(identity, out var identityMatch))
            {
                assembliesByPath.Add(normalizedPath, new LoadedPath(identity, fingerprint, identityMatch.Assembly));
                Publish("permanent-identity-already-loaded", requestedIdentity,
                    $"Identity '{identity}' was already loaded from '{identityMatch.Path}'. Returning the initial assembly instead of loading '{normalizedPath}'.");
                return identityMatch.Assembly;
            }

#if NETFRAMEWORK
            var assembly = Assembly.LoadFrom(normalizedPath);
#else
            var assembly = loadContext.LoadFromAssemblyPath(normalizedPath);
#endif
            assembliesByPath.Add(normalizedPath, new LoadedPath(identity, fingerprint, assembly));
            assembliesByIdentity.Add(identity, new SelectedAssembly(normalizedPath, assembly));
            return assembly;
        }
    }

    void Publish(string code, AssemblyName requestedIdentity, string message) =>
        diagnostics?.Publish(new AssemblyIsolationDiagnostic(code, message, requestedIdentity));

    static string ComputeFingerprint(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha256 = SHA256.Create();
        return Convert.ToBase64String(sha256.ComputeHash(stream));
    }

    sealed record LoadedPath(string Identity, string Fingerprint, Assembly Assembly);

    sealed record SelectedAssembly(string Path, Assembly Assembly);
}
