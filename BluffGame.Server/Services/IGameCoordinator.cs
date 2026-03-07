using BluffGame.Server.Models;

namespace BluffGame.Server.Services;

public interface IGameCoordinator
{
    Task StartGameAsync(Room room);
    Task HandlePlayCardsAsync(Room room, string playerId, List<int> cardIndices, Rank claimedRank);
    Task HandleChallengeAsync(Room room, string challengerId);
    void CancelAllTimers(string roomId);
}
