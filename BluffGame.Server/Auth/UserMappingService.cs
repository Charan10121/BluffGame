using System.Collections.Concurrent;

namespace BluffGame.Server.Auth;

/// <summary>
/// Maps Google user IDs to stable player IDs.
/// Same Google account always gets the same PlayerId across sessions.
/// Thread-safe singleton.
/// </summary>
public class UserMappingService
{
    /// <summary>GoogleId → PlayerId</summary>
    private readonly ConcurrentDictionary<string, string> _googleToPlayer = new();

    /// <summary>PlayerId → user profile</summary>
    private readonly ConcurrentDictionary<string, StoredUser> _users = new();

    /// <summary>
    /// Get or create a stable PlayerId for a Google user.
    /// </summary>
    public string GetOrCreatePlayerId(GoogleUser googleUser)
    {
        var playerId = _googleToPlayer.GetOrAdd(googleUser.GoogleId,
            _ => Guid.NewGuid().ToString("N")[..8]);

        _users[playerId] = new StoredUser
        {
            PlayerId = playerId,
            GoogleId = googleUser.GoogleId,
            Name = googleUser.Name,
            Email = googleUser.Email,
            Picture = googleUser.Picture
        };

        return playerId;
    }

    public StoredUser? GetUser(string playerId) =>
        _users.GetValueOrDefault(playerId);

    public string? GetPlayerIdByGoogleId(string googleId) =>
        _googleToPlayer.GetValueOrDefault(googleId);
}

public class StoredUser
{
    public string PlayerId { get; set; } = string.Empty;
    public string GoogleId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Picture { get; set; } = string.Empty;
}
