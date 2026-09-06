using System.Reflection;
using DevTools.Testing.Abstractions;

namespace DevTools.TestAdapter;

/// <summary>
/// Loads the configured MTP sibling and invokes its <c>Register</c> entry.
/// Must not throw: a failing static constructor aborts testhost discovery.
/// </summary>
public static class HostMtpRegistration
{
    public static string? LastError { get; internal set; }

    public static bool Register(
        string assemblyFileName,
        string entryTypeFullName,
        string baseDirectory,
        Func<string, Assembly> assemblyLoader)
    {
        if (string.IsNullOrWhiteSpace(assemblyFileName))
        {
            LastError = "MTP plugin assembly file name is required.";
            return false;
        }

        if (!IsBareFileName(assemblyFileName))
        {
            LastError = $"MTP plugin assembly must be a bare file name. Got '{assemblyFileName.Trim()}'.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(entryTypeFullName))
        {
            LastError = "MTP plugin entry type is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            LastError = "Testhost base directory is required.";
            return false;
        }

        ArgumentNullException.ThrowIfNull(assemblyLoader);

        LastError = null;
        var path = Path.Combine(baseDirectory, assemblyFileName);
        if (!File.Exists(path))
        {
            LastError = $"MTP plugin assembly '{assemblyFileName}' was not found next to the test executable.";
            return false;
        }

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

    private static bool IsBareFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        return string.Equals(Path.GetFileName(trimmed), trimmed, StringComparison.Ordinal)
               && trimmed.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) < 0
               && !Path.IsPathRooted(trimmed);
    }
}
