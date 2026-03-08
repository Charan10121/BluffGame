using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace BluffGame.Server.Auth;

/// <summary>
/// Issues app-level JWTs after Google authentication succeeds.
/// The JWT carries the player's identity for SignalR and hub methods.
/// </summary>
public class JwtTokenService
{
    private readonly AuthSettings _settings;
    private readonly SymmetricSecurityKey _signingKey;

    public JwtTokenService(AuthSettings settings)
    {
        _settings = settings;
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.JwtSecret));
    }

    /// <summary>
    /// Create a JWT containing the player's stable identity claims.
    /// </summary>
    public string GenerateToken(string playerId, string googleId, string name, string email, string picture)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, playerId),
            new Claim("google_id", googleId),
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.Email, email),
            new Claim("picture", picture)
        };

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.JwtIssuer,
            audience: _settings.JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_settings.JwtExpirationHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Token validation parameters — reused by both JwtBearer middleware
    /// and any manual validation calls.
    /// </summary>
    public TokenValidationParameters GetValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = _settings.JwtIssuer,
        ValidateAudience = true,
        ValidAudience = _settings.JwtAudience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = _signingKey,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(2)
    };
}
