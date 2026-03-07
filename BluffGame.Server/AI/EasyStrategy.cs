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

        // Pick 1–3 random cards (or fewer if hand is small)
        int count = Math.Min(rng.Next(1, 4), bot.Hand.Count);
        var indices = Enumerable.Range(0, bot.Hand.Count)
            .OrderBy(_ => rng.Next())
            .Take(count)
            .ToList();

        // Default: claim the rank of the first selected card
        var claimedRank = bot.Hand[indices[0]].Rank;

        // 35% chance to bluff — claim a random different rank
        if (rng.NextDouble() < 0.35)
        {
            Rank bluffRank;
            do { bluffRank = (Rank)rng.Next(1, 14); }
            while (bluffRank == claimedRank);
            claimedRank = bluffRank;
        }

        return (indices, claimedRank);
    }

    public bool DecideChallenge(
        Player bot, Claim claim, GameState state, IReadOnlyList<Player> players)
    {
        // Easy bot challenges only 15% of the time
        return Random.Shared.NextDouble() < 0.15;
    }
}
