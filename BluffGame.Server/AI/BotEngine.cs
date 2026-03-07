using BluffGame.Server.Models;

namespace BluffGame.Server.AI;

/// <summary>
/// Facade over difficulty-specific strategies.
/// Registered as a singleton — stateless, thread-safe.
/// </summary>
public class BotEngine
{
    private readonly Dictionary<BotDifficulty, IBotStrategy> _strategies = new()
    {
        [BotDifficulty.Easy]   = new EasyStrategy(),
        [BotDifficulty.Medium] = new MediumStrategy()
    };

    public (List<int> CardIndices, Rank ClaimedRank) DecidePlay(
        Player bot, GameState state, IReadOnlyList<Player> players)
    {
        var strategy = _strategies[bot.BotDifficulty ?? BotDifficulty.Easy];
        return strategy.DecidePlay(bot, state, players);
    }

    public bool DecideChallenge(
        Player bot, Claim claim, GameState state, IReadOnlyList<Player> players)
    {
        var strategy = _strategies[bot.BotDifficulty ?? BotDifficulty.Easy];
        return strategy.DecideChallenge(bot, claim, state, players);
    }
}
