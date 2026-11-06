using System.Text.RegularExpressions;

namespace DevTools.TUnit.Runtime;

/// <summary>
/// TUnit.Engine catches what escapes its run and publishes it as one node named
/// "Unhandled exception - Type: message", so a bind failure inside the engine would
/// read as a test result. This turns that node back into the exception it was.
/// </summary>
internal static class TUnitEngineBindFailure
{
    private static readonly Regex UnhandledNode = new(
        "^Unhandled exception - (?<type>[A-Za-z]+): (?<message>.*)$",
        RegexOptions.Singleline | RegexOptions.CultureInvariant);

    public static Exception? TryRead(string displayName)
    {
        var match = UnhandledNode.Match(displayName);
        if (!match.Success)
            return null;

        var message = match.Groups["message"].Value;
        if (!message.Contains("TUnit.", StringComparison.Ordinal)
            && !message.Contains("Microsoft.Testing.", StringComparison.Ordinal))
            return null;

        return match.Groups["type"].Value switch
        {
            nameof(MissingMethodException) => new MissingMethodException(message),
            nameof(MissingFieldException) => new MissingFieldException(message),
            nameof(MissingMemberException) => new MissingMemberException(message),
            nameof(TypeLoadException) => new TypeLoadException(message),
            nameof(FileNotFoundException) => new FileNotFoundException(message),
            nameof(FileLoadException) => new FileLoadException(message),
            _ => null,
        };
    }
}
