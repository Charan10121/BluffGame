namespace BluffGame.Server.Models;

public class GameState
{
    public List<Card> Pile { get; set; } = new();
    public Claim? LastClaim { get; set; }
    public int CurrentPlayerIndex { get; set; }
    public TurnPhase Phase { get; set; } = TurnPhase.PlayCards;
    public int TurnNumber { get; set; } = 1;
    public string? WinnerId { get; set; }
    public ChallengeResult? LastChallengeResult { get; set; }
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
