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

    event EventHandler<AuthStateChangedArgs>? StateChanged;
}

public sealed record AuthResult(bool Success, string? Error = null);

public sealed class AuthStateChangedArgs(bool isAuthenticated) : EventArgs
{
    public bool IsAuthenticated { get; } = isAuthenticated;
}
