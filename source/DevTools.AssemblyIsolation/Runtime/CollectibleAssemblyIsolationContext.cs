#if NET
using System.Reflection;
using System.Runtime.Loader;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Identity;
using DevTools.AssemblyIsolation.Loading;
using DevTools.AssemblyIsolation.Sources;

namespace DevTools.AssemblyIsolation.Runtime;

internal sealed class CollectibleAssemblyIsolationContext : AssemblyLoadContext
{
    readonly AssemblyIsolationPlan plan;

    public CollectibleAssemblyIsolationContext(AssemblyIsolationPlan plan)
        : base($"DevTools.AssemblyIsolation:{Path.GetFileNameWithoutExtension(plan.EntryAssemblyPath)}", isCollectible: true)
    {
        this.plan = plan ?? throw new ArgumentNullException(nameof(plan));
    }

    public Assembly LoadEntryAssembly()
    {
        var requested = AssemblyName.GetAssemblyName(plan.EntryAssemblyPath);
        if (plan.ParentBindings.TryResolve(requested, out var parent))
            return parent;

        return AssemblyStreamLoader.Load(this, plan.EntryAssemblyPath);
    }

    public Assembly LoadAssembly(byte[] assemblyBytes)
    {
        if (assemblyBytes is null) throw new ArgumentNullException(nameof(assemblyBytes));
        using var assemblyStream = new MemoryStream(assemblyBytes, writable: false);
        return LoadFromStream(assemblyStream);
    }

    internal nint ResolveNativeForTesting(string unmanagedDllName) => LoadUnmanagedDll(unmanagedDllName);

    internal Assembly? ResolveManagedForTesting(AssemblyName assemblyName) => Load(assemblyName);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (plan.ParentBindings.TryResolve(assemblyName, out var parent))
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

        Publish("managed-clr-fallback", assemblyName, null, "No private source produced a candidate; delegating to the CLR binder.");
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

    static bool TryValidateCandidate(AssemblyName requested, AssemblyCandidate candidate, out string? rejection)
    {
        if (!File.Exists(candidate.Path))
        {
            rejection = "Candidate file does not exist.";
            return false;
        }

        if (!AssemblyCandidate.IsExistingPathUnderAllowedRoot(candidate.Path, candidate.AllowedRoot))
        {
            rejection = "Candidate is outside its allowed root.";
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

    static bool TryValidateNativeCandidate(string unmanagedDllName, AssemblyCandidate candidate, out string? rejection)
    {
        if (!File.Exists(candidate.Path))
        {
            rejection = $"Native candidate for '{unmanagedDllName}' does not exist.";
            return false;
        }

        if (!AssemblyCandidate.IsExistingPathUnderAllowedRoot(candidate.Path, candidate.AllowedRoot))
        {
            rejection = $"Native candidate for '{unmanagedDllName}' is outside its allowed root.";
            return false;
        }

        rejection = null;
        return true;
    }

    void Publish(string code, AssemblyName? requested, AssemblyCandidate? candidate, string reason)
    {
        var source = candidate is null ? "" : $", source '{candidate.SourceName}', candidate '{candidate.Path}'";
        var identity = requested?.FullName ?? "native library";
        plan.DiagnosticSink?.Publish(new AssemblyIsolationDiagnostic(code, $"Requested '{identity}'{source}: {reason}", requested));
    }
}
#endif
