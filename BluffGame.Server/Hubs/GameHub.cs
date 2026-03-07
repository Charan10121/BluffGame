using BluffGame.Server.Game;
using BluffGame.Server.Models;
using BluffGame.Server.Models.DTOs;
using BluffGame.Server.Services;
using Microsoft.AspNetCore.SignalR;

namespace BluffGame.Server.Hubs;

/// <summary>
/// Central SignalR hub — handles all client↔server communication.
/// Delegates game logic to GameCoordinator; room/session logic to RoomManager.
/// </summary>
public class GameHub : Hub<IGameClient>
{
    private readonly IRoomManager _rooms;
    private readonly IGameCoordinator _coordinator;
    private readonly GameEngine _engine;
    private readonly ILogger<GameHub> _logger;

    public GameHub(
        IRoomManager rooms,
        IGameCoordinator coordinator,
        GameEngine engine,
        ILogger<GameHub> logger)
    {
        _rooms = rooms;
        _coordinator = coordinator;
        _engine = engine;
        _logger = logger;
    }

    // ── Identity ─────────────────────────────────────────────────────────

    /// <summary>Register a new player. Returns their unique player ID.</summary>
    public Task<string> SetPlayerInfo(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new HubException("Name is required.");

        var session = _rooms.RegisterPlayer(Context.ConnectionId, name.Trim());
        _logger.LogInformation("Player registered: {Name} ({PlayerId})", session.PlayerName, session.PlayerId);
        return Task.FromResult(session.PlayerId);
    }

    /// <summary>Attempt to reconnect with a previously assigned player ID.</summary>
    public async Task<ReconnectResult> AttemptReconnect(string playerId, string name)
    {
        var session = _rooms.GetSessionByPlayerId(playerId);

        if (session is null)
        {
            // Session expired — register fresh
            session = _rooms.RegisterPlayer(Context.ConnectionId, name.Trim());
            return new ReconnectResult { Success = false };
        }

        // Update connection mapping
        _rooms.UpdateConnectionId(playerId, Context.ConnectionId);
        session.PlayerName = name.Trim();

        if (session.RoomId is null)
            return new ReconnectResult { Success = true };

        var room = _rooms.GetRoom(session.RoomId);
        if (room is null)
        {
            session.RoomId = null;
            return new ReconnectResult { Success = true };
        }

        // Rejoin SignalR group
        await Groups.AddToGroupAsync(Context.ConnectionId, room.Id);

        // Mark player as reconnected
        var player = room.Players.Find(p => p.Id == playerId);
        if (player is not null)
        {
            player.IsConnected = true;
            player.ConnectionId = Context.ConnectionId;
            player.DisconnectedAt = null;

            await Clients.Group(room.Id).PlayerReconnected(player.Name);
        }

        var details = _rooms.ToRoomDetails(room);
        PlayerGameView? gameState = null;

        if (room.Status == RoomStatus.Playing && room.GameState is not null)
            gameState = _engine.GetPlayerView(room, playerId);

        return new ReconnectResult
        {
            Success = true,
            Room = details,
            GameState = gameState
        };
    }

    // ── Lobby ────────────────────────────────────────────────────────────

    public async Task GetRooms()
    {
        var rooms = _rooms.GetRoomList();
        await Clients.Caller.RoomList(rooms);
    }

    public async Task CreateRoom(CreateRoomRequest request)
    {
        var session = GetRequiredSession();

        // Leave any current room first
        await InternalLeaveRoom(session);

        var room = _rooms.CreateRoom(session, request);
        await Groups.AddToGroupAsync(Context.ConnectionId, room.Id);

        _logger.LogInformation("Room created: {RoomName} ({RoomId}) by {Player}",
            room.Name, room.Id, session.PlayerName);

        await Clients.Caller.RoomJoined(_rooms.ToRoomDetails(room));
        await BroadcastLobbyUpdate();
    }

    public async Task JoinRoom(string roomId)
    {
        var session = GetRequiredSession();

        // Leave any current room first
        await InternalLeaveRoom(session);

        var result = _rooms.JoinRoom(session, roomId);
        if (result is null)
        {
            await Clients.Caller.Error("Cannot join room — it may be full or already started.");
            return;
        }

        var (room, player) = result.Value;
        await Groups.AddToGroupAsync(Context.ConnectionId, room.Id);

        _logger.LogInformation("{Player} joined room {RoomId}", session.PlayerName, room.Id);

        await Clients.Caller.RoomJoined(_rooms.ToRoomDetails(room));
        await Clients.Group(room.Id).RoomUpdated(_rooms.ToRoomDetails(room));
        await Clients.Group(room.Id).Notification($"{player.Name} joined the room.");
        await BroadcastLobbyUpdate();
    }

    public async Task LeaveRoom()
    {
        var session = GetRequiredSession();
        await InternalLeaveRoom(session);
        await BroadcastLobbyUpdate();
    }

    // ── Game ─────────────────────────────────────────────────────────────

    public async Task StartGame()
    {
        var session = GetRequiredSession();
        var room = GetRequiredRoom(session);

        if (room.HostPlayerId != session.PlayerId)
            throw new HubException("Only the host can start the game.");

        if (room.Status != RoomStatus.Waiting)
            throw new HubException("Game has already started.");

        if (room.Players.Count < 2)
            throw new HubException("Need at least 2 players to start.");

        _logger.LogInformation("Game started in room {RoomId}", room.Id);

        await _coordinator.StartGameAsync(room);
        await Clients.Group(room.Id).Notification("🎴 Game started! Good luck!");
        await BroadcastLobbyUpdate();
    }

    public async Task PlayCards(PlayCardsRequest request)
    {
        var session = GetRequiredSession();
        var room = GetRequiredRoom(session);

        if (room.Status != RoomStatus.Playing || room.GameState is null)
            throw new HubException("Game is not in progress.");

        if (!Enum.TryParse<Rank>(request.ClaimedRank, true, out var rank))
            throw new HubException("Invalid rank.");

        await _coordinator.HandlePlayCardsAsync(room, session.PlayerId, request.CardIndices, rank);
    }

    public async Task Challenge()
    {
        var session = GetRequiredSession();
        var room = GetRequiredRoom(session);

        if (room.Status != RoomStatus.Playing || room.GameState is null)
            throw new HubException("Game is not in progress.");

        await _coordinator.HandleChallengeAsync(room, session.PlayerId);
    }

    // ── Connection lifecycle ─────────────────────────────────────────────

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var session = _rooms.GetSessionByConnectionId(Context.ConnectionId);
        if (session is null) return;

        if (session.RoomId is not null)
        {
            var room = _rooms.GetRoom(session.RoomId);
            if (room is not null)
            {
                var player = room.Players.Find(p => p.Id == session.PlayerId);
                if (player is not null)
                {
                    if (room.Status == RoomStatus.Playing)
                    {
                        // Mark disconnected — give grace period for reconnection
                        player.IsConnected = false;
                        player.DisconnectedAt = DateTime.UtcNow;
                        await Clients.Group(room.Id).PlayerDisconnected(player.Name);
                        await Clients.Group(room.Id).Notification(
                            $"⚠️ {player.Name} disconnected. Waiting for reconnection...");
                    }
                    else
                    {
                        // Game not started — just remove from room
                        var (updatedRoom, _) = _rooms.LeaveRoom(session);
                        if (updatedRoom is not null)
                        {
                            await Clients.Group(updatedRoom.Id)
                                .RoomUpdated(_rooms.ToRoomDetails(updatedRoom));
                            await Clients.Group(updatedRoom.Id)
                                .Notification($"{player.Name} left the room.");
                        }
                        await BroadcastLobbyUpdate();
                    }
                }
            }
        }

        _logger.LogInformation("Player disconnected: {ConnectionId}", Context.ConnectionId);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private PlayerSession GetRequiredSession()
    {
        return _rooms.GetSessionByConnectionId(Context.ConnectionId)
            ?? throw new HubException("Not registered. Call SetPlayerInfo first.");
    }

    private Room GetRequiredRoom(PlayerSession session)
    {
        if (session.RoomId is null)
            throw new HubException("You are not in a room.");

        return _rooms.GetRoom(session.RoomId)
            ?? throw new HubException("Room not found.");
    }

    private async Task InternalLeaveRoom(PlayerSession session)
    {
        if (session.RoomId is null) return;

        var room = _rooms.GetRoom(session.RoomId);
        if (room is null) return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, room.Id);

        var (updatedRoom, leftPlayer) = _rooms.LeaveRoom(session);
        if (updatedRoom is not null && leftPlayer is not null)
        {
            await Clients.Group(updatedRoom.Id)
                .RoomUpdated(_rooms.ToRoomDetails(updatedRoom));
            await Clients.Group(updatedRoom.Id)
                .Notification($"{leftPlayer.Name} left the room.");
        }
    }

    private async Task BroadcastLobbyUpdate()
    {
        var rooms = _rooms.GetRoomList();
        await Clients.All.RoomList(rooms);
    }
}
