using System.Reflection;

namespace DevTools.AssemblyIsolation.Identity;

public sealed class AssemblyMismatchException : InvalidOperationException
{
    public AssemblyMismatchException(AssemblyName requested, AssemblyName candidate)
        : base(CreateMessage(requested, candidate))
    {
        Requested = requested;
        Candidate = candidate;
    }

    public AssemblyName Requested { get; }

    public AssemblyName Candidate { get; }

    private static string CreateMessage(AssemblyName requested, AssemblyName candidate)
    {
        if (requested is null) throw new ArgumentNullException(nameof(requested));
        if (candidate is null) throw new ArgumentNullException(nameof(candidate));

        return $"Assembly identity mismatch. Requested '{requested.FullName}', but parent binding is '{candidate.FullName}'.";
    }
}
