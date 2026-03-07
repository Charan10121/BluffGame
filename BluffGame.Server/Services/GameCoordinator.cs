using System.Collections.Concurrent;
using BluffGame.Server.AI;
using BluffGame.Server.Game;
using BluffGame.Server.Hubs;
using BluffGame.Server.Models;
using BluffGame.Server.Models.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace BluffGame.Server.Services;

/// <summary>
/// Orchestrates game flow: timers, bot actions, and state broadcasting.
/// Uses IHubContext to send messages from outside the Hub's request scope.
/// </summary>
public class GameCoordinator : IGameCoordinator
{
    private readonly GameEngine _engine;
    private readonly BotEngine _botEngine;
    private readonly IRoomManager _roomManager;
    private readonly IHubContext<GameHub, IGameClient> _hub;
    private readonly ILogger<GameCoordinator> _logger;

    /// <summary>One active CTS per room — cancelled when a new phase begins.</summary>
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _timers = new();

    private const int ResolutionDelayMs = 3000;

    public GameCoordinator(
        GameEngine engine,
        BotEngine botEngine,
        IRoomManager roomManager,
        IHubContext<GameHub, IGameClient> hub,
        ILogger<GameCoordinator> logger)
    {
        _engine = engine;
        _botEngine = botEngine;
        _roomManager = roomManager;
        _hub = hub;
        _logger = logger;
    }

    // ── Public API ───────────────────────────────────────────────────────

    public async Task StartGameAsync(Room room)
    {
        await room.Semaphore.WaitAsync();
        try { _engine.StartGame(room); }
        finally { room.Semaphore.Release(); }

        await BroadcastGameStateAsync(room);
        await BeginCurrentTurnAsync(room);
    }

    public async Task HandlePlayCardsAsync(
        Room room, string playerId, List<int> cardIndices, Rank claimedRank)
    {
        CancelTimer(room.Id);

        bool success;
        string error;

        await room.Semaphore.WaitAsync();
        try
        {
            (success, error) = _engine.PlayCards(room, playerId, cardIndices, claimedRank);
        }
        finally { room.Semaphore.Release(); }

        if (!success)
        {
            await SendErrorToPlayer(playerId, error);
            return;
        }

        var player = room.Players.Find(p => p.Id == playerId);
        await BroadcastGameStateAsync(room);
        await NotifyRoom(room.Id,
            $"{player?.Name} played {cardIndices.Count} card(s) and claimed {claimedRank}.");

        // Start challenge window (runs in background)
        _ = RunChallengeWindowAsync(room);
    }

    public async Task HandleChallengeAsync(Room room, string challengerId)
    {
        CancelTimer(room.Id);

        bool success;
        ChallengeResult? result;
        string error;

        await room.Semaphore.WaitAsync();
        try
        {
            (success, result, error) = _engine.Challenge(room, challengerId);
        }
        finally { room.Semaphore.Release(); }

        if (!success)
        {
            await SendErrorToPlayer(challengerId, error);
            return;
        }

        var challenger = room.Players.Find(p => p.Id == challengerId);
        var challenged = room.Players.Find(p => p.Id == result!.ChallengedPlayerId);
        var loser = room.Players.Find(p => p.Id == result!.LoserId);

        await BroadcastGameStateAsync(room);

        string bluffText = result!.WasBluff ? "It WAS a bluff!" : "It was NOT a bluff!";
        await NotifyRoom(room.Id,
            $"🔥 {challenger?.Name} challenged {challenged?.Name}! " +
            $"{bluffText} {loser?.Name} picks up {result.PileSize} cards.");

        // Show resolution for a few seconds, then advance
        _ = RunResolutionDelayAsync(room);
    }

    public void CancelAllTimers(string roomId) => CancelTimer(roomId);

    // ── Challenge window ─────────────────────────────────────────────────

    private async Task RunChallengeWindowAsync(Room room)
    {
        int windowMs = room.Settings.ChallengeWindowSeconds * 1000;
        var cts = SetTimer(room.Id);

        // Tell clients the window is open
        await _hub.Clients.Group(room.Id)
            .ChallengeWindowStarted(room.Settings.ChallengeWindowSeconds);

        // Schedule bot challenge decisions
        ScheduleBotChallenges(room, cts.Token);

        try
        {
            await Task.Delay(windowMs, cts.Token);

            // Window expired — no challenge. Advance.
            await AdvanceAndContinueAsync(room);
        }
        catch (OperationCanceledException) { /* A challenge was made — timer cancelled */ }
        catch (Exception ex) { _logger.LogError(ex, "Challenge window error in room {Id}", room.Id); }
    }

    // ── Resolution delay (show revealed cards) ───────────────────────────

    private async Task RunResolutionDelayAsync(Room room)
    {
        var cts = SetTimer(room.Id);

        try
        {
            await Task.Delay(ResolutionDelayMs, cts.Token);
            await AdvanceAndContinueAsync(room);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogError(ex, "Resolution delay error in room {Id}", room.Id); }
    }

    // ── Turn management ──────────────────────────────────────────────────

    private async Task AdvanceAndContinueAsync(Room room)
    {
        await room.Semaphore.WaitAsync();
        try { _engine.AdvanceTurn(room); }
        finally { room.Semaphore.Release(); }

        await BroadcastGameStateAsync(room);

        if (room.Status == RoomStatus.Finished)
        {
            var winner = room.Players.Find(p => p.Id == room.GameState!.WinnerId);
            await _hub.Clients.Group(room.Id)
                .GameOver(winner?.Id ?? "", winner?.Name ?? "Unknown");
            await NotifyRoom(room.Id, $"🏆 {winner?.Name} wins the game!");
            return;
        }

        await BeginCurrentTurnAsync(room);
    }

    private async Task BeginCurrentTurnAsync(Room room)
    {
        var currentPlayer = room.Players[room.GameState!.CurrentPlayerIndex];

        if (currentPlayer.Type == PlayerType.Bot)
        {
            _ = RunBotPlayAsync(room);
        }
        else
        {
            // Start human turn timer
            var cts = SetTimer(room.Id);
            await _hub.Clients.Group(room.Id)
                .TurnTimerStarted(room.Settings.TurnTimeoutSeconds);

            _ = RunTurnTimeoutAsync(room, cts);
        }
    }

    // ── Bot play ─────────────────────────────────────────────────────────

    private async Task RunBotPlayAsync(Room room)
    {
        try
        {
            // Humanised delay
            await Task.Delay(Random.Shared.Next(1200, 2800));

            if (room.Status != RoomStatus.Playing) return;
            if (room.GameState?.Phase != TurnPhase.PlayCards) return;

            var bot = room.Players[room.GameState.CurrentPlayerIndex];
            if (bot.Type != PlayerType.Bot) return;

            var (indices, rank) = _botEngine.DecidePlay(bot, room.GameState, room.Players);

            await HandlePlayCardsAsync(room, bot.Id, indices, rank);
        }
        catch (Exception ex) { _logger.LogError(ex, "Bot play error in room {Id}", room.Id); }
    }

    private void ScheduleBotChallenges(Room room, CancellationToken windowToken)
    {
        if (room.GameState?.LastClaim is null) return;

        var claimerId = room.GameState.LastClaim.PlayerId;
        var bots = room.Players
            .Where(p => p.Type == PlayerType.Bot && p.Id != claimerId && p.Hand.Count > 0)
            .ToList();

        foreach (var bot in bots)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    int delay = Random.Shared.Next(800, room.Settings.ChallengeWindowSeconds * 700);
                    await Task.Delay(delay, windowToken);

                    if (room.GameState.Phase != TurnPhase.ChallengeWindow) return;

                    bool shouldChallenge = _botEngine.DecideChallenge(
                        bot, room.GameState.LastClaim, room.GameState, room.Players);

                    if (shouldChallenge && room.GameState.Phase == TurnPhase.ChallengeWindow)
                    {
                        await HandleChallengeAsync(room, bot.Id);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { _logger.LogError(ex, "Bot challenge error"); }
            }, windowToken);
        }
    }

    // ── Turn timeout ─────────────────────────────────────────────────────

    private async Task RunTurnTimeoutAsync(Room room, CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(room.Settings.TurnTimeoutSeconds * 1000, cts.Token);

            // Timed out — auto-play the first card for the human player
            if (room.Status != RoomStatus.Playing) return;
            if (room.GameState?.Phase != TurnPhase.PlayCards) return;

            var player = room.Players[room.GameState.CurrentPlayerIndex];
            if (player.Hand.Count == 0) return;

            await NotifyRoom(room.Id, $"⏰ {player.Name} ran out of time — auto-playing.");
            await HandlePlayCardsAsync(room, player.Id, new List<int> { 0 }, player.Hand[0].Rank);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogError(ex, "Turn timeout error in room {Id}", room.Id); }
    }

    // ── Broadcasting ─────────────────────────────────────────────────────

    private async Task BroadcastGameStateAsync(Room room)
    {
        foreach (var player in room.Players.Where(p =>
            p.Type == PlayerType.Human && p.IsConnected && p.ConnectionId is not null))
        {
            var view = _engine.GetPlayerView(room, player.Id);
            await _hub.Clients.Client(player.ConnectionId!)
                .GameStateUpdated(view);
        }
    }

    private async Task NotifyRoom(string roomId, string message) =>
        await _hub.Clients.Group(roomId).Notification(message);

    private async Task SendErrorToPlayer(string playerId, string error)
    {
        var session = _roomManager.GetSessionByPlayerId(playerId);
        if (session?.ConnectionId is not null)
            await _hub.Clients.Client(session.ConnectionId).Error(error);
    }

    // ── Timer management ─────────────────────────────────────────────────

    private CancellationTokenSource SetTimer(string roomId)
    {
        CancelTimer(roomId);
        var cts = new CancellationTokenSource();
        _timers[roomId] = cts;
        return cts;
    }

    private void CancelTimer(string roomId)
    {
        if (_timers.TryRemove(roomId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }
}
