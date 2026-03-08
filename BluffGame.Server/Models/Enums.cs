namespace BluffGame.Server.Models;

public enum Suit
{
    Hearts,
    Diamonds,
    Clubs,
    Spades,
    Joker
}

public enum Rank
{
    Joker = 0,
    Ace = 1,
    Two,
    Three,
    Four,
    Five,
    Six,
    Seven,
    Eight,
    Nine,
    Ten,
    Jack,
    Queen,
    King
}

public enum RoomStatus
{
    Waiting,
    Playing,
    Finished
}

public enum BotDifficulty
{
    Easy,
    Medium
}

public enum PlayerType
{
    Human,
    Bot
}

public enum TurnPhase
{
    PlayCards,
    ChallengeWindow,
    Resolving
}
