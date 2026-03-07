using BluffGame.Server.Models;
using BluffGame.Server.Models.DTOs;

namespace BluffGame.Server.Game;

/// <summary>
/// Pure, deterministic game logic — no I/O, no timers, no SignalR.
/// The single source of truth for rule enforcement and state transitions.
/// </summary>
public class GameEngine
{
    // ── Initialisation ───────────────────────────────────────────────────

    public void StartGame(Room room)
    {
        var deck = Deck.CreateStandardDeck();
        Deck.Shuffle(deck);

        foreach (var player in room.Players)
            player.Hand.Clear();

        Deck.Deal(deck, room.Players);

        foreach (var player in room.Players)
            Deck.SortHand(player.Hand);

        room.GameState = new GameState
        {
            CurrentPlayerIndex = 0,
            Phase = TurnPhase.PlayCards,
            TurnNumber = 1
        };

        room.Status = RoomStatus.Playing;
        room.LastActivityAt = DateTime.UtcNow;
    }

    // ── Play cards ───────────────────────────────────────────────────────

    public (bool Success, string Error) PlayCards(
        Room room, string playerId, List<int> cardIndices, Rank claimedRank)
    {
        var state = room.GameState!;
        var player = room.Players.Find(p => p.Id == playerId);

        if (player is null)
            return (false, "Player not found.");

        if (state.Phase != TurnPhase.PlayCards)
            return (false, "Not in the play phase.");

        if (room.Players[state.CurrentPlayerIndex].Id != playerId)
            return (false, "It is not your turn.");

        if (cardIndices.Count is 0 or > 4)
            return (false, "You must play 1–4 cards.");

        if (cardIndices.Any(i => i < 0 || i >= player.Hand.Count))
            return (false, "Invalid card selection.");

        if (cardIndices.Distinct().Count() != cardIndices.Count)
            return (false, "Duplicate card selection.");

        // Extract cards from hand (remove in descending index order to preserve positions)
        var sortedIndices = cardIndices.OrderByDescending(i => i).ToList();
        var playedCards = new List<Card>(sortedIndices.Count);

        foreach (var idx in sortedIndices)
        {
            playedCards.Add(player.Hand[idx]);
            player.Hand.RemoveAt(idx);
        }

        // Add to pile
        state.Pile.AddRange(playedCards);

        // Record the claim
        state.LastClaim = new Claim
        {
            PlayerId = playerId,
            ClaimedRank = claimedRank,
            CardCount = playedCards.Count,
            ActualCards = playedCards
        };

        state.Phase = TurnPhase.ChallengeWindow;
        state.LastChallengeResult = null;
        room.LastActivityAt = DateTime.UtcNow;

        return (true, string.Empty);
    }

    // ── Challenge ────────────────────────────────────────────────────────

    public (bool Success, ChallengeResult? Result, string Error) Challenge(
        Room room, string challengerId)
    {
        var state = room.GameState!;

        if (state.Phase != TurnPhase.ChallengeWindow)
            return (false, null, "No active challenge window.");

        if (state.LastClaim is null)
            return (false, null, "Nothing to challenge.");

        if (state.LastClaim.PlayerId == challengerId)
            return (false, null, "You cannot challenge your own claim.");

        var challenger = room.Players.Find(p => p.Id == challengerId);
        if (challenger is null)
            return (false, null, "Challenger not found.");

        var claimant = room.Players.First(p => p.Id == state.LastClaim.PlayerId);

        // Determine truth: all played cards must match the claimed rank
        bool wasBluff = !state.LastClaim.ActualCards.All(c => c.Rank == state.LastClaim.ClaimedRank);
        var loser = wasBluff ? claimant : challenger;

        // Loser picks up the entire pile
        loser.Hand.AddRange(state.Pile);
        Deck.SortHand(loser.Hand);
        int pileSize = state.Pile.Count;
        state.Pile.Clear();

        var result = new ChallengeResult
        {
            ChallengerId = challengerId,
            ChallengedPlayerId = state.LastClaim.PlayerId,
            WasBluff = wasBluff,
            LoserId = loser.Id,
            RevealedCards = state.LastClaim.ActualCards,
            PileSize = pileSize
        };

        state.LastChallengeResult = result;
        state.Phase = TurnPhase.Resolving;
        room.LastActivityAt = DateTime.UtcNow;

        return (true, result, string.Empty);
    }

    // ── Advance turn ─────────────────────────────────────────────────────

    public void AdvanceTurn(Room room)
    {
        var state = room.GameState!;

        // Check for a winner — first player with zero cards
        var winner = room.Players.Find(p => p.Hand.Count == 0);
        if (winner is not null)
        {
            state.WinnerId = winner.Id;
            room.Status = RoomStatus.Finished;
            return;
        }

        // Advance to next player who still has cards
        int attempts = 0;
        do
        {
            state.CurrentPlayerIndex = (state.CurrentPlayerIndex + 1) % room.Players.Count;
            attempts++;
        }
        while (room.Players[state.CurrentPlayerIndex].Hand.Count == 0 && attempts < room.Players.Count);

        state.Phase = TurnPhase.PlayCards;
        state.TurnNumber++;
        state.LastClaim = null;
        state.LastChallengeResult = null;
    }

    // ── View projection ──────────────────────────────────────────────────

    /// <summary>
    /// Creates a personalized view of the game for a specific player.
    /// Other players' hands are hidden — only card counts are visible.
    /// </summary>
    public PlayerGameView GetPlayerView(Room room, string playerId)
    {
        var state = room.GameState!;
        var player = room.Players.Find(p => p.Id == playerId);

        var claimant = state.LastClaim is not null
            ? room.Players.Find(p => p.Id == state.LastClaim.PlayerId)
            : null;

        return new PlayerGameView
        {
            RoomId = room.Id,
            Hand = player?.Hand.Select(MapCard).ToList() ?? new(),
            PileCount = state.Pile.Count,
            LastClaim = state.LastClaim is not null ? new ClaimDto
            {
                PlayerId = state.LastClaim.PlayerId,
                PlayerName = claimant?.Name ?? "Unknown",
                ClaimedRank = state.LastClaim.ClaimedRank.ToString(),
                CardCount = state.LastClaim.CardCount
            } : null,
            Players = room.Players.Select(p => new PlayerViewDto
            {
                Id = p.Id,
                Name = p.Name,
                CardCount = p.CardCount,
                IsBot = p.Type == PlayerType.Bot,
                IsConnected = p.IsConnected,
                HasWon = p.HasWon
            }).ToList(),
            CurrentPlayerId = state.CurrentPlayerIndex < room.Players.Count
                ? room.Players[state.CurrentPlayerIndex].Id
                : null,
            Phase = state.Phase.ToString(),
            TurnNumber = state.TurnNumber,
            WinnerId = state.WinnerId,
            LastChallengeResult = state.LastChallengeResult is not null
                ? MapChallengeResult(room, state.LastChallengeResult)
                : null
        };
    }

    // ── Mapping helpers ──────────────────────────────────────────────────

    private static CardDto MapCard(Card card) => new()
    {
        Suit = card.Suit.ToString(),
        Rank = card.Rank.ToString(),
        Display = card.Display,
        SuitSymbol = card.SuitSymbol.ToString(),
        RankSymbol = card.RankSymbol,
        IsRed = card.IsRed
    };

    private static ChallengeResultDto MapChallengeResult(Room room, ChallengeResult result)
    {
        var challenger = room.Players.First(p => p.Id == result.ChallengerId);
        var challenged = room.Players.First(p => p.Id == result.ChallengedPlayerId);
        var loser = room.Players.First(p => p.Id == result.LoserId);

        return new ChallengeResultDto
        {
            ChallengerId = result.ChallengerId,
            ChallengerName = challenger.Name,
            ChallengedPlayerId = result.ChallengedPlayerId,
            ChallengedPlayerName = challenged.Name,
            WasBluff = result.WasBluff,
            LoserId = result.LoserId,
            LoserName = loser.Name,
            RevealedCards = result.RevealedCards.Select(MapCard).ToList(),
            PileSize = result.PileSize
        };
    }
}
