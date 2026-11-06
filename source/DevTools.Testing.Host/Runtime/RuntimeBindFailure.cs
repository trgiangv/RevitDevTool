using DevTools.Testing.Transport;
using System.Reflection;
using System.Text.RegularExpressions;

namespace DevTools.Testing.Host.Runtime;

internal static class RuntimeBindFailure
{
    private const string AdapterName = "RevitDevTool.TestAdapter";
    private const string PlatformNames = @"Microsoft\.Testing\.|Microsoft\.VisualStudio\.TestTools\.";

    public static string? TryExplain(Exception exception, RuntimeManifest manifest, Version found, string adapterVersion)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(found);

        var failure = Unwrap(exception);
        if (!IsBindFailure(failure) || !NamesFrameworkOrPlatform(failure, manifest))
            return null;

        return $"{manifest.Framework} {RuntimeManifest.Display(found)} changed an API that {AdapterName} {adapterVersion} uses (built against {manifest.DisplayFloor()}). " +
               $"Update {AdapterName}, or use {manifest.Framework} {manifest.DisplayFloor()}.";
    }

    private static Exception Unwrap(Exception exception)
    {
        while (exception is TargetInvocationException or TypeInitializationException
               && exception.InnerException is { } inner)
        {
            exception = inner;
        }

        return exception;
    }

    private static bool IsBindFailure(Exception failure) =>
        failure is MissingMemberException or TypeLoadException or FileNotFoundException or FileLoadException;

    private static bool NamesFrameworkOrPlatform(Exception failure, RuntimeManifest manifest)
    {
        var fileName = failure switch
        {
            FileNotFoundException notFound => notFound.FileName,
            FileLoadException load => load.FileName,
            _ => null,
        };
        var family = Regex.Escape(manifest.Assembly.Split('.')[0]);
        return Regex.IsMatch(
            $"{fileName} {failure.Message}",
            $@"(?<![\w.])(?:{family}\w*\.|{PlatformNames})",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
