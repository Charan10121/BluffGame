namespace BluffGame.Server.Models.DTOs;

// ── Personalized game view sent to each player ──────────────────────────

public class PlayerGameView
{
    public string RoomId { get; set; } = string.Empty;
    public List<CardDto> Hand { get; set; } = new();
    public int PileCount { get; set; }
    public ClaimDto? LastClaim { get; set; }
    public List<PlayerViewDto> Players { get; set; } = new();
    public string? CurrentPlayerId { get; set; }
    public string Phase { get; set; } = string.Empty;
    public int TurnNumber { get; set; }
    public string? WinnerId { get; set; }
    public ChallengeResultDto? LastChallengeResult { get; set; }
}

// ── Sub-DTOs ─────────────────────────────────────────────────────────────

public class CardDto
{
    public string Suit { get; set; } = string.Empty;
    public string Rank { get; set; } = string.Empty;
    public string Display { get; set; } = string.Empty;
    public string SuitSymbol { get; set; } = string.Empty;
    public string RankSymbol { get; set; } = string.Empty;
    public bool IsRed { get; set; }
}

public class PlayerViewDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int CardCount { get; set; }
    public bool IsBot { get; set; }
    public bool IsConnected { get; set; }
    public bool HasWon { get; set; }
}

public class ClaimDto
{
    public string PlayerId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string ClaimedRank { get; set; } = string.Empty;
    public int CardCount { get; set; }
}

public class ChallengeResultDto
{
    public string ChallengerId { get; set; } = string.Empty;
    public string ChallengerName { get; set; } = string.Empty;
    public string ChallengedPlayerId { get; set; } = string.Empty;
    public string ChallengedPlayerName { get; set; } = string.Empty;
    public bool WasBluff { get; set; }
    public string LoserId { get; set; } = string.Empty;
    public string LoserName { get; set; } = string.Empty;
    public List<CardDto> RevealedCards { get; set; } = new();
    public int PileSize { get; set; }
}

// ── Room DTOs ────────────────────────────────────────────────────────────

public class RoomSummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int PlayerCount { get; set; }
    public int MaxPlayers { get; set; }
    public int BotCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
}

public class RoomDetails
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string HostPlayerId { get; set; } = string.Empty;
    public List<PlayerViewDto> Players { get; set; } = new();
    public RoomSettingsDto Settings { get; set; } = new();
    public string Status { get; set; } = string.Empty;
}

public class RoomSettingsDto
{
    public int MaxPlayers { get; set; }
    public int BotCount { get; set; }
    public string BotDifficulty { get; set; } = string.Empty;
    public int TurnTimeoutSeconds { get; set; }
    public int ChallengeWindowSeconds { get; set; }
}

// ── Request DTOs ─────────────────────────────────────────────────────────

public class CreateRoomRequest
{
    public string RoomName { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public int MaxPlayers { get; set; } = 4;
    public int BotCount { get; set; } = 0;
    public string BotDifficulty { get; set; } = "Easy";
}

public class PlayCardsRequest
{
    public List<int> CardIndices { get; set; } = new();
    public string ClaimedRank { get; set; } = string.Empty;
}

// ── Reconnect ────────────────────────────────────────────────────────────

public class ReconnectResult
{
    public bool Success { get; set; }
    public RoomDetails? Room { get; set; }
    public PlayerGameView? GameState { get; set; }
}

// ── Player session (server-side tracking) ────────────────────────────────

public class PlayerSession
{
    public string PlayerId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public string? RoomId { get; set; }
}
