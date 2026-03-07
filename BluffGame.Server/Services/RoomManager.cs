using System.Collections.Concurrent;
using BluffGame.Server.Models;
using BluffGame.Server.Models.DTOs;

namespace BluffGame.Server.Services;

/// <summary>
/// In-memory room and player session management.
/// Thread-safe via ConcurrentDictionary.
/// </summary>
public class RoomManager : IRoomManager
{
    private readonly ConcurrentDictionary<string, Room> _rooms = new();
    private readonly ConcurrentDictionary<string, PlayerSession> _sessionsByConnection = new();
    private readonly ConcurrentDictionary<string, PlayerSession> _sessionsByPlayerId = new();

    private static readonly string[] BotNames =
        { "Alice Bot", "Bob Bot", "Charlie Bot", "Diana Bot", "Eve Bot" };

    // -- Session management

    public PlayerSession RegisterPlayer(string connectionId, string playerName)
    {
        var session = new PlayerSession
        {
            PlayerId = Guid.NewGuid().ToString("N")[..8],
            PlayerName = playerName,
            ConnectionId = connectionId
        };

        _sessionsByConnection[connectionId] = session;
        _sessionsByPlayerId[session.PlayerId] = session;
        return session;
    }

    public PlayerSession? GetSessionByConnectionId(string connectionId) =>
        _sessionsByConnection.GetValueOrDefault(connectionId);

    public PlayerSession? GetSessionByPlayerId(string playerId) =>
        _sessionsByPlayerId.GetValueOrDefault(playerId);

    public void UpdateConnectionId(string playerId, string newConnectionId)
    {
        if (_sessionsByPlayerId.TryGetValue(playerId, out var session))
        {
            _sessionsByConnection.TryRemove(session.ConnectionId, out _);
            session.ConnectionId = newConnectionId;
            _sessionsByConnection[newConnectionId] = session;
        }
    }

    public void RemoveSession(string connectionId)
    {
        if (_sessionsByConnection.TryRemove(connectionId, out var session))
        {
            _sessionsByPlayerId.TryRemove(session.PlayerId, out _);
        }
    }

    // -- Room management

    public Room CreateRoom(PlayerSession session, CreateRoomRequest request)
    {
        if (!Enum.TryParse<BotDifficulty>(request.BotDifficulty, true, out var difficulty))
            difficulty = BotDifficulty.Easy;

        var room = new Room
        {
            Name = string.IsNullOrWhiteSpace(request.RoomName)
                ? $"{session.PlayerName}'s Room"
                : request.RoomName.Trim(),
            HostPlayerId = session.PlayerId,
            Settings = new RoomSettings
            {
                MaxPlayers = Math.Clamp(request.MaxPlayers, 2, 6),
                BotCount = Math.Clamp(request.BotCount, 0, request.MaxPlayers - 1),
                BotDifficulty = difficulty
            }
        };

        room.Players.Add(new Player
        {
            Id = session.PlayerId,
            Name = session.PlayerName,
            Type = PlayerType.Human,
            ConnectionId = session.ConnectionId
        });

        for (int i = 0; i < room.Settings.BotCount; i++)
        {
            room.Players.Add(new Player
            {
                Id = $"bot-{room.Id}-{i}",
                Name = BotNames[i % BotNames.Length],
                Type = PlayerType.Bot,
                BotDifficulty = difficulty,
                IsConnected = true
            });
        }

        _rooms[room.Id] = room;
        session.RoomId = room.Id;

        return room;
    }

    public Room? GetRoom(string roomId) =>
        _rooms.GetValueOrDefault(roomId);

    public (Room Room, Player Player)? JoinRoom(PlayerSession session, string roomId)
    {
        if (!_rooms.TryGetValue(roomId, out var room))
            return null;

        if (room.Status != RoomStatus.Waiting)
            return null;

        int humanCount = room.Players.Count(p => p.Type == PlayerType.Human);
        int maxHumans = room.Settings.MaxPlayers - room.Settings.BotCount;

        if (humanCount >= maxHumans)
            return null;

        if (room.Players.Any(p => p.Id == session.PlayerId))
            return null;

        var player = new Player
        {
            Id = session.PlayerId,
            Name = session.PlayerName,
            Type = PlayerType.Human,
            ConnectionId = session.ConnectionId
        };

        room.Players.Add(player);
        room.LastActivityAt = DateTime.UtcNow;
        session.RoomId = room.Id;

        return (room, player);
    }

    public (Room? Room, Player? Player) LeaveRoom(PlayerSession session)
    {
        if (session.RoomId is null || !_rooms.TryGetValue(session.RoomId, out var room))
        {
            session.RoomId = null;
            return (null, null);
        }

        var player = room.Players.Find(p => p.Id == session.PlayerId);
        if (player is not null)
        {
            room.Players.Remove(player);
        }

        session.RoomId = null;

        if (!room.Players.Any(p => p.Type == PlayerType.Human))
        {
            _rooms.TryRemove(room.Id, out _);
            return (room, player);
        }

        if (room.HostPlayerId == session.PlayerId)
        {
            var newHost = room.Players.FirstOrDefault(p => p.Type == PlayerType.Human);
            if (newHost is not null)
                room.HostPlayerId = newHost.Id;
        }

        room.LastActivityAt = DateTime.UtcNow;
        return (room, player);
    }

    public void DeleteRoom(string roomId) =>
        _rooms.TryRemove(roomId, out _);

    // -- Queries

    public List<RoomSummary> GetRoomList()
    {
        return _rooms.Values
            .Where(r => r.Status == RoomStatus.Waiting)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r =>
            {
                var host = r.Players.Find(p => p.Id == r.HostPlayerId);
                return new RoomSummary
                {
                    Id = r.Id,
                    Name = r.Name,
                    PlayerCount = r.Players.Count(p => p.Type == PlayerType.Human),
                    MaxPlayers = r.Settings.MaxPlayers,
                    BotCount = r.Settings.BotCount,
                    Status = r.Status.ToString(),
                    HostName = host?.Name ?? "Unknown"
                };
            })
            .ToList();
    }

    public RoomDetails ToRoomDetails(Room room) => new()
    {
        Id = room.Id,
        Name = room.Name,
        HostPlayerId = room.HostPlayerId,
        Players = room.Players.Select(p => new PlayerViewDto
        {
            Id = p.Id,
            Name = p.Name,
            CardCount = p.CardCount,
            IsBot = p.Type == PlayerType.Bot,
            IsConnected = p.IsConnected,
            HasWon = p.HasWon
        }).ToList(),
        Settings = new RoomSettingsDto
        {
            MaxPlayers = room.Settings.MaxPlayers,
            BotCount = room.Settings.BotCount,
            BotDifficulty = room.Settings.BotDifficulty.ToString(),
            TurnTimeoutSeconds = room.Settings.TurnTimeoutSeconds,
            ChallengeWindowSeconds = room.Settings.ChallengeWindowSeconds
        },
        Status = room.Status.ToString()
    };

    public IReadOnlyCollection<Room> GetAllRooms() =>
        _rooms.Values.ToList().AsReadOnly();
}
