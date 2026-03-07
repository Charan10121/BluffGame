using BluffGame.Server.Models;

namespace BluffGame.Server.Game;

/// <summary>
/// Pure utility class for deck creation, shuffling, and dealing.
/// Stateless — all methods operate on provided collections.
/// </summary>
public static class Deck
{
    public static List<Card> CreateStandardDeck()
    {
        var deck = new List<Card>(52);
        foreach (var suit in Enum.GetValues<Suit>())
        {
            foreach (var rank in Enum.GetValues<Rank>())
            {
                deck.Add(new Card(suit, rank));
            }
        }
        return deck;
    }

    /// <summary>Fisher-Yates shuffle — O(n), uniform distribution.</summary>
    public static void Shuffle(List<Card> cards)
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }

    /// <summary>Deal all cards round-robin to players.</summary>
    public static void Deal(List<Card> deck, List<Player> players)
    {
        for (int i = 0; i < deck.Count; i++)
        {
            players[i % players.Count].Hand.Add(deck[i]);
        }
    }

    /// <summary>Sort a hand by rank then suit for consistent display.</summary>
    public static void SortHand(List<Card> hand)
    {
        hand.Sort((a, b) =>
        {
            int cmp = a.Rank.CompareTo(b.Rank);
            return cmp != 0 ? cmp : a.Suit.CompareTo(b.Suit);
        });
    }
}
