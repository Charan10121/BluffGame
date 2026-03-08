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
            RoundStarterIndex = 0,
            ConsecutivePassCount = 0,
            RoundClaimedRank = null,
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

        if (cardIndices.Count is 0)
            return (false, "You must play at least 1 card.");

        if (cardIndices.Any(i => i < 0 || i >= player.Hand.Count))
            return (false, "Invalid card selection.");

        if (cardIndices.Distinct().Count() != cardIndices.Count)
            return (false, "Duplicate card selection.");

        // Once a round starts with a claimed rank, all subsequent plays in that round
        // must keep the same claimed rank until challenge resolution or full-pass reset.
        if (state.RoundClaimedRank.HasValue && claimedRank != state.RoundClaimedRank.Value)
            return (false, $"This round is locked to {state.RoundClaimedRank.Value}. Play that rank or pass.");

        if (!state.RoundClaimedRank.HasValue)
            state.RoundClaimedRank = claimedRank;

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

        // Reset pass counter — someone played
        state.ConsecutivePassCount = 0;

        // Record the claim
        state.LastClaim = new Claim
        {
            PlayerId = playerId,
            ClaimedRank = state.RoundClaimedRank.Value,
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
        // Jokers are wild — they always match
        bool wasBluff = !state.LastClaim.ActualCards.All(
            c => c.Rank == Rank.Joker || c.Rank == state.LastClaim.ClaimedRank);
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

    // ── Pass ────────────────────────────────────────────────────────────

    /// <summary>
    /// Player passes their turn (plays no cards).
    /// If all active players pass consecutively, the pile is cleared and a new round begins.
    /// </summary>
    public (bool Success, bool RoundOver, string Error) Pass(Room room, string playerId)
    {
        var state = room.GameState!;
        var player = room.Players.Find(p => p.Id == playerId);

        if (player is null)
            return (false, false, "Player not found.");

        if (state.Phase != TurnPhase.PlayCards)
            return (false, false, "Not in the play phase.");

        if (room.Players[state.CurrentPlayerIndex].Id != playerId)
            return (false, false, "It is not your turn.");

        state.ConsecutivePassCount++;
        room.LastActivityAt = DateTime.UtcNow;

        int activePlayers = room.Players.Count(p => p.Hand.Count > 0);

        if (state.ConsecutivePassCount >= activePlayers)
        {
            // All active players passed → clear pile, start new round
            state.Pile.Clear();
            state.ConsecutivePassCount = 0;

            // Next player after the round starter begins the new round
            int nextIdx = (state.RoundStarterIndex + 1) % room.Players.Count;
            int attempts = 0;
            while ((room.Players[nextIdx].Hand.Count == 0 || !room.Players[nextIdx].IsConnected) 
                   && attempts < room.Players.Count)
            {
                nextIdx = (nextIdx + 1) % room.Players.Count;
                attempts++;
            }

            state.CurrentPlayerIndex = nextIdx;
            state.RoundStarterIndex = nextIdx;
            state.RoundClaimedRank = null;
            state.LastClaim = null;
            state.TurnNumber++;
            state.Phase = TurnPhase.PlayCards;
            return (true, true, string.Empty);
        }

        // Advance to next active and connected player
        int next = (state.CurrentPlayerIndex + 1) % room.Players.Count;
        int att = 0;
        while ((room.Players[next].Hand.Count == 0 || !room.Players[next].IsConnected) 
               && att < room.Players.Count)
        {
            next = (next + 1) % room.Players.Count;
            att++;
        }
        state.CurrentPlayerIndex = next;
        state.TurnNumber++;
        state.Phase = TurnPhase.PlayCards;

        return (true, false, string.Empty);
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

        if (state.LastChallengeResult is not null)
        {
            // After a challenge: the WINNER of the challenge starts the next round
            var result = state.LastChallengeResult;
            string winnerId = result.WasBluff ? result.ChallengerId : result.ChallengedPlayerId;
            int winnerIdx = room.Players.FindIndex(p => p.Id == winnerId);

            // If the winner has no cards or is disconnected, find next active player
            int attempts = 0;
            while ((room.Players[winnerIdx].Hand.Count == 0 || !room.Players[winnerIdx].IsConnected) 
                   && attempts < room.Players.Count)
            {
                winnerIdx = (winnerIdx + 1) % room.Players.Count;
                attempts++;
            }

            state.CurrentPlayerIndex = winnerIdx;
            state.RoundStarterIndex = winnerIdx;
            state.RoundClaimedRank = null;
        }
        else
        {
            // Normal advance (no challenge) — move to next active and connected player
            int attempts = 0;
            do
            {
                state.CurrentPlayerIndex = (state.CurrentPlayerIndex + 1) % room.Players.Count;
                attempts++;
            }
            while ((room.Players[state.CurrentPlayerIndex].Hand.Count == 0 
                    || !room.Players[state.CurrentPlayerIndex].IsConnected) 
                   && attempts < room.Players.Count);
        }

        state.Phase = TurnPhase.PlayCards;
        state.TurnNumber++;
        state.ConsecutivePassCount = 0;
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
            RoundClaimedRank = state.RoundClaimedRank?.ToString(),
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
