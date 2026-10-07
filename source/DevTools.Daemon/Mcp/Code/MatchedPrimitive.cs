namespace DevTools.Daemon.Mcp.Code;

/// <summary>
/// One primitive that matched a <c>code_mode</c> search. Score stays off this type.
/// </summary>
public sealed record MatchedPrimitive(string Name, string? Description, int ProcessId, string PrimitiveType);
