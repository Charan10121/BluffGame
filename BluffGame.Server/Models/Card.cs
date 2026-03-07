namespace BluffGame.Server.Models;

public record Card(Suit Suit, Rank Rank)
{
    public string Display => $"{Rank} of {Suit}";

    public char SuitSymbol => Suit switch
    {
        Suit.Hearts   => '♥',
        Suit.Diamonds => '♦',
        Suit.Clubs    => '♣',
        Suit.Spades   => '♠',
        _             => '?'
    };

    public string RankSymbol => Rank switch
    {
        Rank.Ace   => "A",
        Rank.Jack  => "J",
        Rank.Queen => "Q",
        Rank.King  => "K",
        _          => ((int)Rank).ToString()
    };

    public bool IsRed => Suit is Suit.Hearts or Suit.Diamonds;
}
