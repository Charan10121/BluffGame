namespace BluffGame.Server.Auth;

// ── Request / Response DTOs for auth endpoints ──────────────────────────

public class GoogleLoginRequest
{
    public string IdToken { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Picture { get; set; } = string.Empty;
}

// ── Internal model for validated Google user ────────────────────────────

public class GoogleUser
{
    public string GoogleId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Picture { get; set; } = string.Empty;
}

// ── Configuration ───────────────────────────────────────────────────────

public class AuthSettings
{
    public string GoogleClientId { get; set; } = string.Empty;
    public string JwtSecret { get; set; } = string.Empty;
    public string JwtIssuer { get; set; } = "BluffGame";
    public string JwtAudience { get; set; } = "BluffGame";
    public int JwtExpirationHours { get; set; } = 24;
}
