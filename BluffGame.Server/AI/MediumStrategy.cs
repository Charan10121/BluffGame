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

        // Group hand by rank, pick the largest group (truthful play)
        var rankGroups = bot.Hand
            .Select((card, index) => (card, index))
            .GroupBy(x => x.card.Rank)
            .OrderByDescending(g => g.Count())
            .ToList();

        var bestGroup = rankGroups.First();
        var indices = bestGroup.Take(4).Select(x => x.index).ToList();
        var claimedRank = bestGroup.Key;

        // 25% chance to bluff instead
        if (rng.NextDouble() < 0.25)
        {
            int count = Math.Min(rng.Next(1, 3), bot.Hand.Count);
            indices = Enumerable.Range(0, bot.Hand.Count)
                .OrderBy(_ => rng.Next())
                .Take(count)
                .ToList();

            // Pick a rank that does NOT match all selected cards
            var selectedCards = indices.Select(i => bot.Hand[i]).ToList();
            bool allMatch;
            do
            {
                claimedRank = (Rank)rng.Next(1, 14);
                allMatch = selectedCards.All(c => c.Rank == claimedRank);
            }
            while (allMatch && selectedCards.Count > 0);
        }

        return (indices, claimedRank);
    }

    public bool DecideChallenge(
        Player bot, Claim claim, GameState state, IReadOnlyList<Player> players)
    {
        // Count how many of the claimed rank the bot holds
        int botHas = bot.Hand.Count(c => c.Rank == claim.ClaimedRank);

        // Max the claimer could genuinely hold = 4 total in deck minus what we hold
        int maxPossible = 4 - botHas;

        // Definite bluff — they claim more than is mathematically possible
        if (claim.CardCount > maxPossible)
            return true;

        // Build a suspicion score [0, 1]
        double suspicion = 0;

        // The more of that rank we hold, the less likely the claim is true
        suspicion += botHas * 0.25;              // 0 – 1.0

        // Large claims are riskier / more suspicious
        suspicion += claim.CardCount * 0.08;     // 0.08 – 0.32

        // Small pile → low risk to challenge → more willing
        if (state.Pile.Count < 6)
            suspicion += 0.10;

        // Challenge probability = suspicion * scaling factor
        return Random.Shared.NextDouble() < suspicion * 0.65;
    }
}
