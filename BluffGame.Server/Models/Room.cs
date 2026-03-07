namespace BluffGame.Server.Models;

public class Room
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = string.Empty;
    public string HostPlayerId { get; set; } = string.Empty;
    public List<Player> Players { get; set; } = new();
    public GameState? GameState { get; set; }
    public RoomSettings Settings { get; set; } = new();
    public RoomStatus Status { get; set; } = RoomStatus.Waiting;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>Async-safe lock for game state mutations.</summary>
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
}

public class RoomSettings
{
    public int MaxPlayers { get; set; } = 4;
    public int BotCount { get; set; } = 0;
    public BotDifficulty BotDifficulty { get; set; } = BotDifficulty.Easy;
    public int TurnTimeoutSeconds { get; set; } = 30;
    public int ChallengeWindowSeconds { get; set; } = 10;
}
