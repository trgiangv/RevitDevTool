#if NET
using System.Reflection;
using System.Runtime.Loader;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Identity;
using DevTools.AssemblyIsolation.Loading;
using DevTools.AssemblyIsolation.Sources;

namespace DevTools.AssemblyIsolation.Runtime;

internal sealed class AssemblyIsolationContext(AssemblyIsolationPlan plan) : 
    AssemblyLoadContext($"DevTools.AssemblyIsolation:{Path.GetFileNameWithoutExtension(plan.EntryAssemblyPath)}", isCollectible: true)
{
    private readonly AssemblyIsolationPlan plan = plan ?? throw new ArgumentNullException(nameof(plan));

    public Assembly LoadEntryAssembly()
    {
        var requested = AssemblyName.GetAssemblyName(plan.EntryAssemblyPath);
        return plan.TryShare(requested, out var parent) 
            ? parent 
            : AssemblyStreamLoader.Load(this, plan.EntryAssemblyPath);
    }

    public Assembly LoadAssembly(byte[] assemblyBytes, byte[]? symbolBytes = null)
    {
        ArgumentNullException.ThrowIfNull(assemblyBytes);
        using var assemblyStream = new MemoryStream(assemblyBytes, writable: false);
        if (symbolBytes is not { Length: > 0 }) 
            return LoadFromStream(assemblyStream);
        using var symbolStream = new MemoryStream(symbolBytes, writable: false);
        return LoadFromStream(assemblyStream, symbolStream);
    }

    internal nint ResolveNativeForTesting(string name) => LoadUnmanagedDll(name);

    internal Assembly? ResolveManagedForTesting(AssemblyName assemblyName) => Load(assemblyName);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (plan.TryShare(assemblyName, out var parent))
            return parent;

        foreach (var source in plan.ManagedSources)
        {
            var candidate = source.Resolve(assemblyName);
            if (candidate is null)
                continue;

            if (!TryValidateCandidate(assemblyName, candidate, out var rejection))
            {
                Publish("managed-candidate-rejected", assemblyName, candidate, rejection!);
                continue;
            }

            Publish("managed-candidate-selected", assemblyName, candidate, "Loading private candidate.");
            return AssemblyStreamLoader.Load(this, candidate.Path);
        }

        return null;
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        foreach (var source in plan.NativeSources)
        {
            var candidate = source.Resolve(unmanagedDllName);
            if (candidate is null)
                continue;

            if (!TryValidateNativeCandidate(unmanagedDllName, candidate, out var rejection))
            {
                Publish("native-candidate-rejected", null, candidate, rejection!);
                continue;
            }

            Publish("native-candidate-selected", null, candidate, $"Loading native candidate for '{unmanagedDllName}'.");
            return LoadUnmanagedDllFromPath(candidate.Path);
        }

        return nint.Zero;
    }

    private static bool TryValidateCandidate(AssemblyName requested, AssemblyCandidate candidate, out string? rejection)
    {
        if (!File.Exists(candidate.Path))
        {
            rejection = "Candidate file does not exist.";
            return false;
        }

        if (!AssemblyCandidate.IsExistingPathUnderRoot(candidate.Path, candidate.Root))
        {
            rejection = "Candidate is outside its root.";
            return false;
        }

        var candidateIdentity = AssemblyName.GetAssemblyName(candidate.Path);
        if (!AssemblyIdentityMatcher.IsCompatible(requested, candidateIdentity))
        {
            rejection = $"Candidate identity '{candidateIdentity.FullName}' is incompatible.";
            return false;
        }

        rejection = null;
        return true;
    }

    private static bool TryValidateNativeCandidate(string name, AssemblyCandidate candidate, out string? rejection)
    {
        if (!File.Exists(candidate.Path))
        {
            rejection = $"Native candidate for '{name}' does not exist.";
            return false;
        }

        if (!AssemblyCandidate.IsExistingPathUnderRoot(candidate.Path, candidate.Root))
        {
            rejection = $"Native candidate for '{name}' is outside its root.";
            return false;
        }

        rejection = null;
        return true;
    }

    private void Publish(string code, AssemblyName? requested, AssemblyCandidate? candidate, string reason)
    {
        var detail = candidate is null ? "" : $", candidate '{candidate.Path}'";
        var identity = requested?.FullName ?? "native library";
        plan.DiagnosticSink?.Publish(new AssemblyIsolationDiagnostic(code, $"Requested '{identity}'{detail}: {reason}", requested));
    }
}
#endif
