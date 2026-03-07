using BluffGame.Server.Models;

namespace BluffGame.Server.Services;

/// <summary>
/// Background service that periodically removes stale / abandoned rooms.
/// </summary>
public class RoomCleanupService : BackgroundService
{
    private readonly IRoomManager _roomManager;
    private readonly IGameCoordinator _coordinator;
    private readonly ILogger<RoomCleanupService> _logger;

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan WaitingTimeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan FinishedTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DisconnectGrace = TimeSpan.FromSeconds(90);

    public RoomCleanupService(
        IRoomManager roomManager,
        IGameCoordinator coordinator,
        ILogger<RoomCleanupService> logger)
    {
        _roomManager = roomManager;
        _coordinator = coordinator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                CleanRooms();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Room cleanup error");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private void CleanRooms()
    {
        var now = DateTime.UtcNow;

        foreach (var room in _roomManager.GetAllRooms())
        {
            bool shouldDelete = room.Status switch
            {
                RoomStatus.Waiting  => now - room.LastActivityAt > WaitingTimeout,
                RoomStatus.Finished => now - room.LastActivityAt > FinishedTimeout,
                RoomStatus.Playing  => !room.Players.Any(p => p.Type == PlayerType.Human && p.IsConnected),
                _                   => false
            };

            if (shouldDelete)
            {
                _logger.LogInformation("Cleaning up room {RoomId} ({Status})", room.Id, room.Status);
                _coordinator.CancelAllTimers(room.Id);
                _roomManager.DeleteRoom(room.Id);
            }
        }
    }
}
