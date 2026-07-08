namespace BlackjackBot.Domain.Entities;

public class Card
{
    public Suit Suit { get; }
    public Rank Rank { get; }

    public Card(Suit suit, Rank rank)
    {
        Suit = suit;
        Rank = rank;
    }

    public override string ToString()
    {
        var suitStr = Suit switch
        {
            Suit.Hearts => "♥️",
            Suit.Diamonds => "♦️",
            Suit.Clubs => "♣️",
            Suit.Spades => "♠️",
            _ => ""
        };

        var rankStr = Rank switch
        {
            Rank.Jack => "J",
            Rank.Queen => "Q",
            Rank.King => "K",
            Rank.Ace => "A",
            _ => ((int)Rank).ToString()
        };

        return $"{suitStr}{rankStr}";
    }
}
