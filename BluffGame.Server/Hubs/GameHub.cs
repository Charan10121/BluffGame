using System.Security.Claims;
using BluffGame.Server.Game;
using BluffGame.Server.Models;
using BluffGame.Server.Models.DTOs;
using BluffGame.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BluffGame.Server.Hubs;

/// <summary>
/// Central SignalR hub — handles all client↔server communication.
/// Delegates game logic to GameCoordinator; room/session logic to RoomManager.
/// Kept thin: transport + identity extraction only. All rules live in Engine/Coordinator.
/// </summary>
[Authorize]
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

    // ── Identity helpers ─────────────────────────────────────────────────

    /// <summary>Extract the stable PlayerId from JWT claims.</summary>
    private string GetAuthenticatedPlayerId() =>
        Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new HubException("Authentication required.");

    private string GetAuthenticatedName() =>
        Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";

    // ── Identity ─────────────────────────────────────────────────────────

    /// <summary>Register/re-register the authenticated player for this connection.</summary>
    public Task<string> SetPlayerInfo(string name)
    {
        var playerId = GetAuthenticatedPlayerId();
        var displayName = string.IsNullOrWhiteSpace(name) ? GetAuthenticatedName() : name.Trim();

        var session = _rooms.RegisterOrUpdatePlayer(Context.ConnectionId, playerId, displayName);
        _logger.LogInformation("Player registered: {Name} ({PlayerId})", session.PlayerName, session.PlayerId);
        return Task.FromResult(session.PlayerId);
    }

    /// <summary>Attempt to reconnect with the authenticated player's identity.</summary>
    public async Task<ReconnectResult> AttemptReconnect(string playerId, string name)
    {
        // Always use the JWT-provided PlayerId — ignore client-supplied one
        var authPlayerId = GetAuthenticatedPlayerId();
        var displayName = string.IsNullOrWhiteSpace(name) ? GetAuthenticatedName() : name.Trim();

        var session = _rooms.GetSessionByPlayerId(authPlayerId);

        if (session is null)
        {
            // No prior session — register fresh
            session = _rooms.RegisterOrUpdatePlayer(Context.ConnectionId, authPlayerId, displayName);
            return new ReconnectResult { Success = false, PlayerId = session.PlayerId };
        }

        // Update connection mapping
        _rooms.UpdateConnectionId(authPlayerId, Context.ConnectionId);
        session.PlayerName = displayName;

        if (session.RoomId is null)
            return new ReconnectResult { Success = true, PlayerId = authPlayerId };

        var room = _rooms.GetRoom(session.RoomId);
        if (room is null)
        {
            session.RoomId = null;
            return new ReconnectResult { Success = true, PlayerId = authPlayerId };
        }

        // Rejoin SignalR group
        await Groups.AddToGroupAsync(Context.ConnectionId, room.Id);

        // Mark player as reconnected
        var player = room.Players.Find(p => p.Id == authPlayerId);
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
            gameState = _engine.GetPlayerView(room, authPlayerId);

        return new ReconnectResult
        {
            Success = true,
            PlayerId = authPlayerId,
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

        // All validation delegated to coordinator
        await _coordinator.StartGameAsync(room, session.PlayerId);
        await Clients.Group(room.Id).Notification("🎴 Game started! Good luck!");
        await BroadcastLobbyUpdate();
    }

    public async Task PlayCards(PlayCardsRequest request)
    {
        var session = GetRequiredSession();
        var room = GetRequiredRoom(session);

        // Minimal transport-level parsing; game rules enforced in coordinator/engine
        if (!Enum.TryParse<Rank>(request.ClaimedRank, true, out var rank))
            throw new HubException("Invalid rank.");

        await _coordinator.HandlePlayCardsAsync(room, session.PlayerId, request.CardIndices, rank);
    }

    public async Task Challenge()
    {
        var session = GetRequiredSession();
        var room = GetRequiredRoom(session);
        await _coordinator.HandleChallengeAsync(room, session.PlayerId);
    }

    public async Task Pass()
    {
        var session = GetRequiredSession();
        var room = GetRequiredRoom(session);
        await _coordinator.HandlePassAsync(room, session.PlayerId);
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
        // Prefer JWT PlayerId; fall back to connection-based lookup
        var authPlayerId = GetAuthenticatedPlayerId();
        return _rooms.GetSessionByPlayerId(authPlayerId)
            ?? _rooms.GetSessionByConnectionId(Context.ConnectionId)
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
