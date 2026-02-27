namespace RevitDevTool.Desktop.Models;

public sealed record PlanJobItem(
    string FilePath,
    string HostVersion,
    bool Headless,
    bool CloseDocument,
    bool CloseHost);

public sealed record HostProgressItem(
    string HostLabel,
    string Message,
    int Current,
    int Total,
    double Percent);

public sealed record HostLogItem(
    string Timestamp,
    string Level,
    string Source,
    string Message,
    string? Exception);

public sealed record ResultRowItem(
    int Index,
    bool Success,
    long DurationMs,
    string? Error,
    string? StackTrace);

public sealed record HostInstanceItem(
    string AppId,
    string HostVersion,
    int ProcessId,
    string PipeName);
