namespace BluffGame.Server.Models;

public record Card(Suit Suit, Rank Rank)
{
    public bool IsJoker => Rank == Rank.Joker;

    public string Display => IsJoker ? "Joker" : $"{Rank} of {Suit}";

    public char SuitSymbol => Suit switch
    {
        Suit.Hearts   => '♥',
        Suit.Diamonds => '♦',
        Suit.Clubs    => '♣',
        Suit.Spades   => '♠',
        Suit.Joker    => '★',
        _             => '?'
    };

    public string RankSymbol => Rank switch
    {
        Rank.Joker => "★",
        Rank.Ace   => "A",
        Rank.Jack  => "J",
        Rank.Queen => "Q",
        Rank.King  => "K",
        _          => ((int)Rank).ToString()
    };

    public bool IsRed => Suit is Suit.Hearts or Suit.Diamonds;
}
