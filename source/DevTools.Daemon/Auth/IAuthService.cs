namespace DevTools.Daemon.Auth;

public interface IAuthService
{
    bool IsAuthenticated { get; }
    string? AccessToken { get; }
    string? UserId { get; }
    string? Email { get; }
    string? DisplayName { get; }
    string? AvatarUrl { get; }

    Task<AuthResult> SignInAsync(CancellationToken ct = default);
    Task SignOutAsync();
    Task<bool> RefreshAsync();

    event EventHandler<AuthStateArgs>? StateChanged;
}

public sealed class AuthStateArgs(bool isAuthenticated) : EventArgs
{
    public bool IsAuthenticated { get; } = isAuthenticated;
}

public sealed record AuthResult(bool Success, string? Error = null);
