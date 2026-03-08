using BluffGame.Server.Models;

namespace BluffGame.Server.AI;

/// <summary>
/// Easy bot: plays randomly, rarely bluffs strategically, seldom challenges.
/// Suitable for casual / beginner opponents.
/// </summary>
public class EasyStrategy : IBotStrategy
{
    public (List<int> CardIndices, Rank ClaimedRank) DecidePlay(
        Player bot, GameState state, IReadOnlyList<Player> players)
    {
        var rng = Random.Shared;
        var roundRank = state.RoundClaimedRank;

        if (roundRank.HasValue)
        {
            // Round is locked — must claim this rank.
            // Prefer matching cards + jokers for truthful plays
            var matchingIndices = bot.Hand
                .Select((c, i) => (c, i))
                .Where(x => x.c.Rank == roundRank.Value || x.c.Rank == Rank.Joker)
                .Select(x => x.i)
                .ToList();

            if (matchingIndices.Count > 0)
            {
                // Play 1–3 matching cards
                int count = Math.Min(rng.Next(1, 4), matchingIndices.Count);
                var indices = matchingIndices.OrderBy(_ => rng.Next()).Take(count).ToList();
                return (indices, roundRank.Value);
            }

            // No matching cards — bluff with random cards (35%) or this shouldn't happen
            // because ShouldPass should have caught it, but just in case:
            int bluffCount = Math.Min(rng.Next(1, 3), bot.Hand.Count);
            var bluffIndices = Enumerable.Range(0, bot.Hand.Count)
                .OrderBy(_ => rng.Next())
                .Take(bluffCount)
                .ToList();
            return (bluffIndices, roundRank.Value);
        }

        // New round — pick cards and choose a rank
        int cardCount = Math.Min(rng.Next(1, 4), bot.Hand.Count);
        var selectedIndices = Enumerable.Range(0, bot.Hand.Count)
            .OrderBy(_ => rng.Next())
            .Take(cardCount)
            .ToList();

        // Default: claim the rank of the first non-joker selected card
        var firstNonJoker = selectedIndices.Select(i => bot.Hand[i]).FirstOrDefault(c => c.Rank != Rank.Joker);
        var claimedRank = firstNonJoker?.Rank ?? (Rank)rng.Next(1, 14);

        // 35% chance to bluff — claim a random different rank
        if (rng.NextDouble() < 0.35)
        {
            Rank bluffRank;
            do { bluffRank = (Rank)rng.Next(1, 14); }
            while (bluffRank == claimedRank);
            claimedRank = bluffRank;
        }

        return (selectedIndices, claimedRank);
    }

    public bool DecideChallenge(
        Player bot, Claim claim, GameState state, IReadOnlyList<Player> players)
    {
        // Easy bot challenges only 15% of the time
        return Random.Shared.NextDouble() < 0.15;
    }

    public bool ShouldPass(Player bot, GameState state, IReadOnlyList<Player> players)
    {
        // Must play on the very first turn of a new round (no one has played yet)
        if (!state.RoundClaimedRank.HasValue && state.ConsecutivePassCount == 0)
            return false;

        // If round is locked, check if we even have matching cards
        if (state.RoundClaimedRank.HasValue)
        {
            int matching = bot.Hand.Count(c => c.Rank == state.RoundClaimedRank.Value || c.Rank == Rank.Joker);
            // No matching cards → 85% pass, 15% bluff
            if (matching == 0)
                return Random.Shared.NextDouble() < 0.85;
        }

        // Easy bot passes ~10% of the time otherwise
        return Random.Shared.NextDouble() < 0.10;
    }
}
