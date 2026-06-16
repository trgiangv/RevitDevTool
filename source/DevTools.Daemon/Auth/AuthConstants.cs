namespace DevTools.Daemon.Auth;

public static class AuthConstants
{
    public const string TokenFileName = "auth.dat";
    public const int RefreshBufferSeconds = 300;

    public static class Endpoints
    {
        public const string Revoke = "/oauth/token/revoke";
    }

    public static class JwtClaims
    {
        public const string Subject = "sub";
        public const string Email = "email";
        public const string Name = "name";
        public const string Picture = "picture";
    }
}
