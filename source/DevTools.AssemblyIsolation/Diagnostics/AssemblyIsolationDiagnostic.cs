using System.Reflection;

namespace DevTools.AssemblyIsolation.Diagnostics;

public sealed class AssemblyIsolationDiagnostic
{
    public AssemblyIsolationDiagnostic(string code, string message, AssemblyName? requestedAssembly = null)
    {
        Code = string.IsNullOrWhiteSpace(code)
            ? throw new ArgumentException("A diagnostic code is required.", nameof(code))
            : code;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        RequestedAssembly = requestedAssembly;
    }

    public string Code { get; }

    public string Message { get; }

    public AssemblyName? RequestedAssembly { get; }
}
