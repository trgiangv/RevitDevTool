using DevTools.Utilities;
using Duende.IdentityModel.OidcClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZLogger;

namespace DevTools.Daemon.Auth;

public sealed class AuthService : IAuthService, IDisposable
{
    private const string TokenFileName = "auth.dat";
    private const int RefreshBufferSeconds = 300;
    private const string RevokeEndpoint = "/oauth/token/revoke";
    
    private readonly AuthOptions _options;
    private readonly ILogger _logger;
    private readonly HttpClient _http = new();
    private readonly TokenStore _tokens;

    private TokenData? _tokenData;

    public AuthService(IOptions<AuthOptions> optionsAccessor, ILogger<AuthService> logger)
    {
        _logger = logger;
        _options = optionsAccessor.Value;
        _tokens = new TokenStore(
            Path.Combine(AppUtils.GetApplicationDataPath(), TokenFileName),
            logger);
        _tokenData = _tokens.TryLoad();
    }

    private bool IsConfigured => !string.IsNullOrEmpty(_options.Issuer);

    private OidcClient OidcClient => field ??= new OidcClient(new OidcClientOptions
    {
        Authority = _options.Issuer.TrimEnd('/'),
        ClientId = _options.ClientId,
        Scope = _options.Scope,
        RedirectUri = _options.RedirectUri,
        Browser = new AuthBrowser(_options),
    });

    public bool IsAuthenticated => _tokenData is not null && !string.IsNullOrEmpty(_tokenData.AccessToken);
    public string? AccessToken => _tokenData?.AccessToken;
    public string? UserId => _tokenData?.UserId;
    public string? Email => _tokenData?.Email;
    public string? DisplayName => _tokenData?.DisplayName;
    public string? AvatarUrl => _tokenData?.AvatarUrl;

    public event EventHandler<AuthStateArgs>? StateChanged;

    public Task<AuthResult> SignInAsync(CancellationToken ct = default)
    {
        if (!IsConfigured)
            return Task.FromResult(new AuthResult(false, "Auth not configured — set Issuer and ClientId in appsettings."));
        return PerformOAuthFlowAsync(ct);
    }

    public async Task SignOutAsync()
    {
        if (_tokenData?.AccessToken is { } token && IsConfigured)
            await RevokeAccessTokenAsync(token).ConfigureAwait(false);

        ClearAndNotify();
    }

    public async Task<bool> RefreshAsync()
    {
        if (!IsConfigured || _tokenData is null) return false;
        if (!IsTokenExpiringSoon()) return true;

        if (string.IsNullOrEmpty(_tokenData.RefreshToken))
        {
            ClearAndNotify();
            return false;
        }

        try
        {
            var result = await OidcClient.RefreshTokenAsync(_tokenData.RefreshToken!)
                .ConfigureAwait(false);

            if (result.IsError)
            {
                _logger.ZLogWarning($"Token refresh failed: {result.Error}");
                ClearAndNotify();
                return false;
            }

            _tokenData.AccessToken = result.AccessToken;
            _tokenData.RefreshToken = result.RefreshToken ?? _tokenData.RefreshToken;
            _tokenData.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(result.ExpiresIn).ToUnixTimeSeconds();
            _tokens.Save(_tokenData);
            return true;
        }
        catch (Exception ex)
        {
            _logger.ZLogWarning(ex, $"Token refresh failed");
            return false;
        }
    }

    private async Task<AuthResult> PerformOAuthFlowAsync(CancellationToken ct)
    {
        try
        {
            var result = await OidcClient.LoginAsync(new LoginRequest(), ct).ConfigureAwait(false);

            if (result.IsError)
                return new AuthResult(false, result.Error);

            _tokenData = TokenData.FromLogin(result);
            _tokens.Save(_tokenData);
            StateChanged?.Invoke(this, new AuthStateArgs(true));
            return new AuthResult(true);
        }
        catch (OperationCanceledException)
        {
            return new AuthResult(false, "Cancelled");
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"OAuth flow failed");
            return new AuthResult(false, ex.Message);
        }
    }

    private async Task RevokeAccessTokenAsync(string token)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post,
                $"{OidcClient.Options.Authority}{RevokeEndpoint}")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["token"] = token,
                    ["client_id"] = OidcClient.Options.ClientId,
                })
            };
            await _http.SendAsync(request).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.ZLogWarning(ex, $"Failed to revoke token");
        }
    }

    private bool IsTokenExpiringSoon()
    {
        if (_tokenData?.ExpiresAt is not { } exp) return true;
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp - RefreshBufferSeconds;
    }

    private void ClearAndNotify()
    {
        _tokenData = null;
        _tokens.Delete();
        StateChanged?.Invoke(this, new AuthStateArgs(false));
    }

    public void Dispose() => _http.Dispose();
}
