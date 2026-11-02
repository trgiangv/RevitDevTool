using DevTools.Ipc;
using DevTools.Utilities;

namespace DevTools.Execution.External.Testing;

public static class PytestProtocol
{
    public const string Name = "pytest";
    public const int CurrentVersion = 1;
    public const int MinVersion = 1;

    public const string UpdateClientMessage = "Update revitdevtool_pytest to the latest version.";
    public const string UpdateProductMessage = "Update RevitDevTool to the latest version.";

    public static ProtocolRange Range { get; } = new(CurrentVersion, MinVersion);

    private static readonly ProtocolPeer Host =
        new("RevitDevTool", new ProtocolSender(AppVersion.Current, Range), UpdateProductMessage);

    public static string? Mismatch(PytestRunRequest request)
    {
        var client = new ProtocolPeer("revitdevtool_pytest", request.Sender, UpdateClientMessage);
        return Host.IsCompatible(client) ? null : Host.ExplainMismatch(client);
    }
}