using BluffGame.Server.Models;

namespace BluffGame.Server.Services;

public interface IGameCoordinator
{
    Task StartGameAsync(Room room, string playerId);
    Task HandlePlayCardsAsync(Room room, string playerId, List<int> cardIndices, Rank claimedRank);
    Task HandleChallengeAsync(Room room, string challengerId);
    Task HandlePassAsync(Room room, string playerId);
    void CancelAllTimers(string roomId);
}
