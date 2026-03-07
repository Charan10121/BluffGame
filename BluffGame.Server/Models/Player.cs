namespace BluffGame.Server.Models;

public class Player
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<Card> Hand { get; set; } = new();
    public PlayerType Type { get; set; }
    public BotDifficulty? BotDifficulty { get; set; }
    public string? ConnectionId { get; set; }
    public bool IsConnected { get; set; } = true;
    public DateTime? DisconnectedAt { get; set; }

    public int CardCount => Hand.Count;
    public bool HasWon => Type == PlayerType.Human
        ? Hand.Count == 0
        : Hand.Count == 0;
}
