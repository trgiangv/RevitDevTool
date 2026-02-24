using MessagePack;

namespace RevitDevTool.Bridge.IPC;

/// <summary>
/// Generic envelope for all messages sent over the named pipe.
/// Payload is a MessagePack-serialized inner object (byte[]).
/// Any type annotated with <see cref="MessagePackObjectAttribute"/> can be a payload.
/// </summary>
[MessagePackObject]
public sealed partial class PipeMessage
{
    [Key(0)] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [Key(1)] public PipeMessageType Type { get; set; }
    [Key(2)] public byte[]? Payload { get; set; }
}

public enum PipeMessageType
{
    Ping,
    Pong,
    ExecuteJob,
    Progress,
    LogChunk,
    JobCompleted,
    JobFailed,
    CancelJob,
    Shutdown,
    ShutdownAck
}
