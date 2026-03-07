using BluffGame.Server.Models;
using BluffGame.Server.Models.DTOs;

namespace BluffGame.Server.Services;

public interface IRoomManager
{
    PlayerSession RegisterPlayer(string connectionId, string playerName);
    PlayerSession? GetSessionByConnectionId(string connectionId);
    PlayerSession? GetSessionByPlayerId(string playerId);
    void UpdateConnectionId(string playerId, string newConnectionId);
    void RemoveSession(string connectionId);

    Room CreateRoom(PlayerSession session, CreateRoomRequest request);
    Room? GetRoom(string roomId);
    (Room Room, Player Player)? JoinRoom(PlayerSession session, string roomId);
    (Room? Room, Player? Player) LeaveRoom(PlayerSession session);
    void DeleteRoom(string roomId);

    List<RoomSummary> GetRoomList();
    RoomDetails ToRoomDetails(Room room);

    IReadOnlyCollection<Room> GetAllRooms();
}
