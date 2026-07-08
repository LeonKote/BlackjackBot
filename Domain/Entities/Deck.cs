namespace BlackjackBot.Domain.Entities;

public class Deck
{
    private readonly Stack<Card> _cards;

    public Deck()
    {
        var cards = Enum.GetValues<Suit>().SelectMany(s => Enum.GetValues<Rank>().Select(r => new Card(s, r))).ToArray();
        Random.Shared.Shuffle(cards);
        _cards = new Stack<Card>(cards);
    }

    public Card Draw() => _cards.Pop();
}
