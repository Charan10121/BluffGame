using Google.Apis.Auth;

namespace BluffGame.Server.Auth;

/// <summary>
/// Validates Google ID tokens using Google's public keys.
/// Returns structured user info on success, null on failure.
/// </summary>
public class GoogleTokenValidator
{
    private readonly string _clientId;
    private readonly ILogger<GoogleTokenValidator> _logger;

    public GoogleTokenValidator(AuthSettings settings, ILogger<GoogleTokenValidator> logger)
    {
        _clientId = settings.GoogleClientId;
        _logger = logger;
    }

    public async Task<GoogleUser?> ValidateAsync(string idToken)
    {
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _clientId }
                });

            return new GoogleUser
            {
                GoogleId = payload.Subject,
                Email = payload.Email,
                Name = payload.Name ?? payload.Email,
                Picture = payload.Picture ?? string.Empty
            };
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning(ex, "Invalid Google ID token");
            return null;
        }
    }
}
