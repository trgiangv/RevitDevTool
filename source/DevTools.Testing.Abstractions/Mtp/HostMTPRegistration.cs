using System.Reflection;

namespace DevTools.Testing.Abstractions.MTP;

/// <summary>
/// Loads the selected MTP sibling and invokes its <c>Register</c> entry.
/// Must not throw: a failing static constructor aborts testhost discovery.
/// </summary>
public static class HostMTPRegistration
{
    public const string RequiredMtpAssembliesMessage =
        "DevTools.NUnit.MTP.dll or DevTools.TUnit.MTP.dll";

    public static string? LastError { get; private set; }

    public static bool RegisterForFramework(
        string frameworkId,
        string baseDirectory,
        Func<string, Assembly> assemblyLoader)
    {
        if (string.IsNullOrWhiteSpace(frameworkId))
        {
            LastError = "Framework id is required.";
            return false;
        }

        if (!TryResolvePlugin(frameworkId, out var plugin))
        {
            LastError = $"Testing framework '{frameworkId.Trim()}' is not supported.";
            return false;
        }

        return Register(plugin.AssemblyFileName, plugin.EntryTypeFullName, baseDirectory, assemblyLoader);
    }

    public static bool TryResolvePlugin(
        string frameworkId,
        out (string AssemblyFileName, string EntryTypeFullName) plugin)
    {
        plugin = frameworkId.Trim().ToLowerInvariant() switch
        {
            "nunit" => ("DevTools.NUnit.MTP.dll", "DevTools.NUnit.MTP.NUnitMTP"),
            "tunit" => ("DevTools.TUnit.MTP.dll", "DevTools.TUnit.MTP.TUnitMTP"),
            _ => default,
        };

        return plugin != default;
    }

    private static bool Register(
        string assemblyFileName,
        string entryTypeFullName,
        string baseDirectory,
        Func<string, Assembly> assemblyLoader)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
            throw new ArgumentException("Value is required.", nameof(baseDirectory));
        if (assemblyLoader is null)
            throw new ArgumentNullException(nameof(assemblyLoader));

        LastError = null;
        var path = Path.Combine(baseDirectory, assemblyFileName);
        if (!File.Exists(path))
            return false;

        try
        {
            var assembly = assemblyLoader(path);
            var type = assembly.GetType(entryTypeFullName, throwOnError: false);
            type?.GetMethod("Register", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, null);
            if (HostTestDiscovery.Provider is null)
                LastError = $"{entryTypeFullName}.Register did not assign HostTestDiscovery.Provider.";
        }
        catch (Exception ex)
        {
            var failure = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
            LastError = failure.ToString();
        }

        return HostTestDiscovery.Provider is not null;
    }
}
