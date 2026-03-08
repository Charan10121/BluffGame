using BluffGame.Server.Models;

namespace BluffGame.Server.AI;

/// <summary>
/// Strategy interface for bot decision-making.
/// Each difficulty level provides a different implementation.
/// </summary>
public interface IBotStrategy
{
    /// <summary>Decide which cards to play and what rank to claim.</summary>
    (List<int> CardIndices, Rank ClaimedRank) DecidePlay(
        Player bot, GameState state, IReadOnlyList<Player> players);

    /// <summary>Decide whether to challenge the last claim.</summary>
    bool DecideChallenge(
        Player bot, Claim claim, GameState state, IReadOnlyList<Player> players);

    /// <summary>Decide whether to pass (play no cards) this turn.</summary>
    bool ShouldPass(Player bot, GameState state, IReadOnlyList<Player> players);
}
