using BluffGame.Server.Models.DTOs;

namespace BluffGame.Server.Hubs;

/// <summary>
/// Strongly-typed server → client contract.
/// SignalR uses these method names directly as event names on the client.
/// </summary>
public interface IGameClient
{
    Task PlayerIdAssigned(string playerId);
    Task RoomList(List<RoomSummary> rooms);
    Task RoomJoined(RoomDetails details);
    Task RoomUpdated(RoomDetails details);
    Task GameStateUpdated(PlayerGameView view);
    Task ChallengeWindowStarted(int seconds);
    Task TurnTimerStarted(int seconds);
    Task GameOver(string winnerId, string winnerName);
    Task Error(string message);
    Task Notification(string message);
    Task PlayerDisconnected(string playerName);
    Task PlayerReconnected(string playerName);
}
