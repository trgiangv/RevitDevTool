namespace DevTools.Daemon.Hosting;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class GatewayOptions
{
    public const string SectionName = "Gateway";

    private const string WssScheme = "wss://";
    private const string HttpsScheme = "https://";

    public string Url { get; init; } = string.Empty;

    public string HttpBaseUrl => Url.Replace(WssScheme, HttpsScheme).Replace(GatewayRouteConstants.Tunnel, string.Empty);
}
