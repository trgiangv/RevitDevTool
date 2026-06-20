using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DevTools.Utilities;
using Duende.IdentityModel.OidcClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZLogger;

namespace DevTools.Daemon.Auth;

/// <summary>
/// OAuth 2.1 Authorization Code + PKCE via Duende OidcClient (RFC 8252).
/// Opens system browser for login, receives callback on 127.0.0.1.
/// Tokens stored encrypted with DPAPI.
/// </summary>
public sealed class AuthService : IAuthService, IDisposable
{
    private readonly AuthOptions _options;
    private readonly ILogger _logger;
    private readonly HttpClient _http = new();
    private readonly string _tokenFilePath;

    private TokenData? _tokenData;

    public AuthService(IOptions<AuthOptions> optionsAccessor, ILogger<AuthService> logger)
    {
        _logger = logger;
        _options = optionsAccessor.Value;
        _tokenFilePath = Path.Combine(AppUtils.GetApplicationDataPath(), AuthConstants.TokenFileName);
        LoadStoredToken();
    }

    private bool IsConfigured => !string.IsNullOrEmpty(_options.Issuer);

    private OidcClient OidcClient => field ??= new OidcClient(new OidcClientOptions
    {
        Authority = _options.Issuer.TrimEnd('/'),
        ClientId = _options.ClientId,
        Scope = _options.Scope,
        RedirectUri = _options.RedirectUri,
        Browser = new LoopbackBrowser(_options),
    });

    public bool IsAuthenticated => _tokenData is not null && !string.IsNullOrEmpty(_tokenData.AccessToken);
    public string? AccessToken => _tokenData?.AccessToken;
    public string? UserId => _tokenData?.UserId;
    public string? Email => _tokenData?.Email;
    public string? DisplayName => _tokenData?.DisplayName;
    public string? AvatarUrl => _tokenData?.AvatarUrl;

    public event EventHandler<AuthStateChangedArgs>? StateChanged;

    public Task<AuthResult> SignInAsync(CancellationToken ct = default)
    {
        if (!IsConfigured)
            return Task.FromResult(new AuthResult(false, "Auth not configured — set Issuer and ClientId in appsettings."));
        return PerformOAuthFlowAsync(ct);
    }

    public async Task SignOutAsync()
    {
        if (_tokenData?.AccessToken is { } token && IsConfigured)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post,
                    $"{OidcClient.Options.Authority}{AuthConstants.Endpoints.Revoke}")
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

        _tokenData = null;
        DeleteStoredToken();
        StateChanged?.Invoke(this, new AuthStateChangedArgs(false));
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
            SaveToken();
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

            _tokenData = new TokenData
            {
                AccessToken = result.AccessToken,
                RefreshToken = result.RefreshToken,
                ExpiresAt = result.AccessTokenExpiration.ToUnixTimeSeconds(),
                UserId = result.User?.FindFirst(AuthConstants.JwtClaims.Subject)?.Value,
                Email = result.User?.FindFirst(AuthConstants.JwtClaims.Email)?.Value,
                DisplayName = result.User?.FindFirst(AuthConstants.JwtClaims.Name)?.Value,
                AvatarUrl = result.User?.FindFirst(AuthConstants.JwtClaims.Picture)?.Value,
            };

            SaveToken();
            StateChanged?.Invoke(this, new AuthStateChangedArgs(true));
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

    private bool IsTokenExpiringSoon()
    {
        if (_tokenData?.ExpiresAt is not { } exp) return true;
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp - AuthConstants.RefreshBufferSeconds;
    }

    private void ClearAndNotify()
    {
        _tokenData = null;
        DeleteStoredToken();
        StateChanged?.Invoke(this, new AuthStateChangedArgs(false));
    }

    private void SaveToken()
    {
        if (_tokenData is null) return;
        try
        {
            var json = JsonSerializer.Serialize(_tokenData);
            var plainBytes = Encoding.UTF8.GetBytes(json);
            var encrypted = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(_tokenFilePath, encrypted);
        }
        catch (Exception ex)
        {
            _logger.ZLogWarning(ex, $"Failed to save auth token");
        }
    }

    private void LoadStoredToken()
    {
        try
        {
            if (!File.Exists(_tokenFilePath)) return;
            var encrypted = File.ReadAllBytes(_tokenFilePath);
            var plainBytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(plainBytes);
            _tokenData = JsonSerializer.Deserialize<TokenData>(json);
        }
        catch (Exception ex)
        {
            _logger.ZLogWarning(ex, $"Failed to load stored auth token");
            _tokenData = null;
        }
    }

    private void DeleteStoredToken()
    {
        try
        {
            File.Delete(_tokenFilePath);
        }
        catch
        {
             /* best effort */
        }
    }

    public void Dispose() => _http.Dispose();
}
