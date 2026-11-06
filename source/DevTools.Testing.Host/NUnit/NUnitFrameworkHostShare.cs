using DevTools.AssemblyIsolation.Identity;
using DevTools.AssemblyIsolation.Loading;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Transport;
using System.Diagnostics;
using System.Reflection;
#if NET
using System.Runtime.Loader;
#endif

namespace DevTools.Testing.Host.NUnit;

/// <summary>
/// Loads the user's <c>nunit.framework</c> once into the host context, because NUnit statics
/// and <c>AsyncLocal</c> roots are per process. A later run with another file version stops.
/// A copy that another add-in loaded is reused only when its identity and file version both match.
/// </summary>
internal static class NUnitFrameworkHostShare
{
    private const string FrameworkSimpleName = "nunit.framework";

    private static readonly Lock Gate = new();
    private static Assembly? _shared;
    private static string? _sharedFileVersion;

    internal static bool IsFrameworkSimpleName(string? simpleName) =>
        string.Equals(simpleName, FrameworkSimpleName, StringComparison.OrdinalIgnoreCase);

    internal static Assembly GetOrLoadFromShadow(string shadowFrameworkPath, string hostName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shadowFrameworkPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostName);

        var fullPath = Path.GetFullPath(shadowFrameworkPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Generation nunit.framework shadow path not found: {fullPath}",
                fullPath);
        }

        var identity = AssemblyName.GetAssemblyName(fullPath);
        if (!IsFrameworkSimpleName(identity.Name))
        {
            throw new InvalidOperationException(
                $"Shadow path '{fullPath}' is not nunit.framework (identity '{identity.FullName}').");
        }

        var fileVersion = ReadFileVersion(fullPath, identity);
        lock (Gate)
        {
            if (_shared is not null)
            {
                if (!AssemblyIdentityMatcher.IsCompatible(identity, _shared.GetName())
                    || !string.Equals(fileVersion, _sharedFileVersion, StringComparison.Ordinal))
                {
                    throw new TestingRuntimeIncompatibleException(SecondVersion(hostName, _sharedFileVersion!, fileVersion));
                }

                return _shared;
            }

            _shared = FindOrLoad(fullPath, identity, fileVersion);
            _sharedFileVersion = fileVersion;
            return _shared;
        }
    }

    internal static bool TryGetLoaded(out Assembly assembly)
    {
        lock (Gate)
        {
            assembly = _shared!;
            return _shared is not null;
        }
    }

    internal static bool TryFindSameCopy(
        IEnumerable<Assembly> assemblies,
        AssemblyName required,
        string fileVersion,
        out Assembly assembly)
    {
        foreach (var loaded in assemblies)
        {
            var name = loaded.GetName();
            if (!IsFrameworkSimpleName(name.Name)
                || !AssemblyIdentityMatcher.IsCompatible(required, name)
                || !string.Equals(fileVersion, ReadFileVersion(loaded), StringComparison.Ordinal))
            {
                continue;
            }

            assembly = loaded;
            return true;
        }

        assembly = null!;
        return false;
    }

    private static Assembly FindOrLoad(string fullPath, AssemblyName identity, string fileVersion)
    {
#if NET
        var hostContext = AssemblyLoadContext.GetLoadContext(typeof(NUnitFrameworkHostShare).Assembly)
            ?? AssemblyLoadContext.Default;

        if (TryFindSameCopy(hostContext.Assemblies, identity, fileVersion, out var fromHost))
            return fromHost;

        if (!ReferenceEquals(hostContext, AssemblyLoadContext.Default)
            && TryFindSameCopy(AssemblyLoadContext.Default.Assemblies, identity, fileVersion, out var fromDefault))
        {
            return fromDefault;
        }

        return AssemblyStreamLoader.Load(hostContext, fullPath);
#else
        return TryFindSameCopy(AppDomain.CurrentDomain.GetAssemblies(), identity, fileVersion, out var fromDomain)
            ? fromDomain
            : AssemblyStreamLoader.Load(fullPath);
#endif
    }

    private static string ReadFileVersion(string path, AssemblyName identity) =>
        FileVersionInfo.GetVersionInfo(path).FileVersion ?? identity.Version?.ToString() ?? string.Empty;

    private static string ReadFileVersion(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
        ?? assembly.GetName().Version?.ToString()
        ?? string.Empty;

    private static string SecondVersion(string hostName, string loaded, string requested) =>
        $"{hostName} has NUnit {Display(loaded)} loaded from an earlier run. Restart {hostName} to run tests with NUnit {Display(requested)}.";

    private static string Display(string fileVersion) =>
        Version.TryParse(fileVersion, out var version) ? RuntimeManifest.Display(version) : fileVersion;
}
