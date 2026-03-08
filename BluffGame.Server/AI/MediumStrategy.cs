using BluffGame.Server.Models;

namespace BluffGame.Server.AI;

/// <summary>
/// Medium bot: prefers truthful plays, tracks card probabilities to detect bluffs.
/// Bluffs strategically ~25% of the time.
/// </summary>
public class MediumStrategy : IBotStrategy
{
    public (List<int> CardIndices, Rank ClaimedRank) DecidePlay(
        Player bot, GameState state, IReadOnlyList<Player> players)
    {
        var rng = Random.Shared;
        var roundRank = state.RoundClaimedRank;

        // Separate jokers from normal cards
        var jokerIndices = bot.Hand
            .Select((card, idx) => (card, idx))
            .Where(x => x.card.Rank == Rank.Joker)
            .Select(x => x.idx)
            .ToList();

        if (roundRank.HasValue)
        {
            // Round is locked — must claim this rank.
            // Prefer matching cards + jokers for truthful plays
            var matchingIndices = bot.Hand
                .Select((c, i) => (c, i))
                .Where(x => x.c.Rank == roundRank.Value)
                .Select(x => x.i)
                .ToList();

            var allUsable = matchingIndices.Concat(jokerIndices).ToList();

            if (allUsable.Count > 0)
            {
                int count = Math.Min(rng.Next(1, 4), allUsable.Count);
                var indices = allUsable.OrderBy(_ => rng.Next()).Take(count).ToList();
                return (indices, roundRank.Value);
            }

            // No matching cards or jokers — bluff with random cards
            int bluffCount = Math.Min(rng.Next(1, 3), bot.Hand.Count);
            var bluffIndices = Enumerable.Range(0, bot.Hand.Count)
                .OrderBy(_ => rng.Next())
                .Take(bluffCount)
                .ToList();
            return (bluffIndices, roundRank.Value);
        }

        // New round — use original strategy to pick best group
        var rankGroups = bot.Hand
            .Select((card, index) => (card, index))
            .Where(x => x.card.Rank != Rank.Joker)
            .GroupBy(x => x.card.Rank)
            .OrderByDescending(g => g.Count())
            .ToList();

        if (rankGroups.Count == 0)
        {
            // Only jokers — play one and claim a random rank
            return (new List<int> { jokerIndices[0] }, (Rank)rng.Next(1, 14));
        }

        var bestGroup = rankGroups.First();
        var indices2 = bestGroup.Take(4).Select(x => x.index).ToList();
        var claimedRank = bestGroup.Key;

        // Pad with jokers if available (jokers are wild — always truthful)
        foreach (var ji in jokerIndices)
        {
            if (indices2.Count >= 4) break;
            indices2.Add(ji);
        }

        // 25% chance to bluff instead
        if (rng.NextDouble() < 0.25)
        {
            int count = Math.Min(rng.Next(1, 3), bot.Hand.Count);
            indices2 = Enumerable.Range(0, bot.Hand.Count)
                .OrderBy(_ => rng.Next())
                .Take(count)
                .ToList();

            // Pick a rank that does NOT match all selected non-joker cards
            var selectedCards = indices2.Select(i => bot.Hand[i]).Where(c => c.Rank != Rank.Joker).ToList();
            bool allMatch;
            do
            {
                claimedRank = (Rank)rng.Next(1, 14);
                allMatch = selectedCards.All(c => c.Rank == claimedRank);
            }
            while (allMatch && selectedCards.Count > 0);
        }

        return (indices2, claimedRank);
    }

    public bool DecideChallenge(
        Player bot, Claim claim, GameState state, IReadOnlyList<Player> players)
    {
        // Count how many of the claimed rank the bot holds
        int botHas = bot.Hand.Count(c => c.Rank == claim.ClaimedRank);
        int botJokers = bot.Hand.Count(c => c.Rank == Rank.Joker);

        // Max the claimer could genuinely hold = 4 of that rank + 2 jokers, minus what we hold
        int maxPossible = (4 - botHas) + (2 - botJokers);

        // Definite bluff — they claim more than is mathematically possible
        if (claim.CardCount > maxPossible)
            return true;

        // Build a suspicion score [0, 1]
        double suspicion = 0;

        // The more of that rank we hold, the less likely the claim is true
        suspicion += botHas * 0.20;

        // Large claims are riskier / more suspicious
        suspicion += claim.CardCount * 0.08;

        // Small pile → low risk to challenge → more willing
        if (state.Pile.Count < 6)
            suspicion += 0.10;

        // Challenge probability = suspicion * scaling factor
        return Random.Shared.NextDouble() < suspicion * 0.65;
    }

    public bool ShouldPass(Player bot, GameState state, IReadOnlyList<Player> players)
    {
        // Must play on the very first turn of a new round (no one has played yet)
        if (!state.RoundClaimedRank.HasValue && state.ConsecutivePassCount == 0)
            return false;

        // If round is locked, check if we have matching cards
        if (state.RoundClaimedRank.HasValue)
        {
            int matching = bot.Hand.Count(c => c.Rank == state.RoundClaimedRank.Value || c.Rank == Rank.Joker);
            // No matching cards → 70% pass, 30% bluff (medium bot bluffs more)
            if (matching == 0)
                return Random.Shared.NextDouble() < 0.70;
        }

        // Pass more often with larger hands
        if (bot.Hand.Count > 15) return Random.Shared.NextDouble() < 0.25;
        return Random.Shared.NextDouble() < 0.08;
    }
}
