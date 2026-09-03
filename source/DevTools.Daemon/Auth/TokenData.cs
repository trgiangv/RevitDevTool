using Duende.IdentityModel;
using Duende.IdentityModel.OidcClient;

namespace DevTools.Daemon.Auth;

public sealed class TokenData
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public long? ExpiresAt { get; set; }
    public string? UserId { get; init; }
    public string? Email { get; init; }
    public string? DisplayName { get; init; }
    public string? AvatarUrl { get; init; }

    public static TokenData FromLogin(LoginResult result) => new()
    {
        AccessToken = result.AccessToken,
        RefreshToken = result.RefreshToken,
        ExpiresAt = result.AccessTokenExpiration.ToUnixTimeSeconds(),
        UserId = result.User?.FindFirst(JwtClaimTypes.Subject)?.Value,
        Email = result.User?.FindFirst(JwtClaimTypes.Email)?.Value,
        DisplayName = result.User?.FindFirst(JwtClaimTypes.Name)?.Value,
        AvatarUrl = result.User?.FindFirst(JwtClaimTypes.Picture)?.Value,
    };
}
