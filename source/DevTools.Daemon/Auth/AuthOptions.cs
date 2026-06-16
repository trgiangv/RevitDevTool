namespace DevTools.Daemon.Auth;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string Issuer { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public int LoopbackPort { get; init; } = 17823;
    public string Scope { get; init; } = "openid profile email offline_access";

    public string UriPrefix => $"http://127.0.0.1:{LoopbackPort}/";
    public string RedirectUri => $"{UriPrefix}callback";
}
