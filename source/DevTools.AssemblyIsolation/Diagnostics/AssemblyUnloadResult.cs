namespace DevTools.AssemblyIsolation.Diagnostics;

public sealed record AssemblyUnloadResult(bool IsCollectible, bool IsUnloaded, string? Detail);
