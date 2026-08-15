using System.Diagnostics;

namespace DevTools.NUnit.Mtp;

internal sealed class SystemDebugSession : IDebugSession
{
    internal static SystemDebugSession Instance { get; } = new();

    public bool IsAttached => Debugger.IsAttached;

    public int ProcessId => Environment.ProcessId;
}
