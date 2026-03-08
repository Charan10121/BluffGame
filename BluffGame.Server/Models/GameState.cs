namespace BluffGame.Server.Models;

public class GameState
{
    public List<Card> Pile { get; set; } = new();
    public Claim? LastClaim { get; set; }
    public Rank? RoundClaimedRank { get; set; }
    public int CurrentPlayerIndex { get; set; }
    public TurnPhase Phase { get; set; } = TurnPhase.PlayCards;
    public int TurnNumber { get; set; } = 1;
    public string? WinnerId { get; set; }
    public ChallengeResult? LastChallengeResult { get; set; }

    /// <summary>Index of the player who started the current round.</summary>
    public int RoundStarterIndex { get; set; }

    /// <summary>Number of consecutive passes since the last play. When this reaches the active player count, the pile is cleared.</summary>
    public int ConsecutivePassCount { get; set; }
}

public class Claim
{
    public string PlayerId { get; set; } = string.Empty;
    public Rank ClaimedRank { get; set; }
    public int CardCount { get; set; }

    /// <summary>Hidden from other players — the actual cards placed on the pile.</summary>
    public List<Card> ActualCards { get; set; } = new();
}

public class ChallengeResult
{
    public string ChallengerId { get; set; } = string.Empty;
    public string ChallengedPlayerId { get; set; } = string.Empty;
    public bool WasBluff { get; set; }
    public string LoserId { get; set; } = string.Empty;
    public List<Card> RevealedCards { get; set; } = new();
    public int PileSize { get; set; }
}
