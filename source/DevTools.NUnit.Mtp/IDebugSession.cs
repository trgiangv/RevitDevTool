namespace DevTools.NUnit.Mtp;

internal interface IDebugSession
{
    bool IsAttached { get; }

    int ProcessId { get; }
}
