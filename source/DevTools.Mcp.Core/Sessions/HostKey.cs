namespace DevTools.Mcp.Core;

public readonly record struct HostKey(string MachineId, int ProcessId)
{
    public override string ToString() => $"{MachineId}:{ProcessId}";
}
