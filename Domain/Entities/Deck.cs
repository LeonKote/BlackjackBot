using System.Security.Cryptography;
using System.Text;

namespace BlackjackBot.Domain.Entities;

public class Deck
{
    private readonly Stack<Card> _cards;

    public Deck(string serverSeed, string clientSeed, long gameId) // <-- Теперь принимает gameId
    {
        var initialDeck = Enum.GetValues<Suit>()
            .SelectMany(s => Enum.GetValues<Rank>().Select(r => new Card(s, r)))
            .ToList();

        var seedString = $"{serverSeed}:{clientSeed}:{gameId}"; // <-- Используем gameId

        var cardsWithHashes = initialDeck.Select((card, index) =>
        {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{seedString}:{index}"));
            return new { Card = card, Hash = Convert.ToHexString(hashBytes).ToLower() };
        }).ToList();

        var sortedByHash = cardsWithHashes.OrderBy(x => x.Hash).Select(x => x.Card).ToList();
        sortedByHash.Reverse();
        _cards = new Stack<Card>(sortedByHash);
    }

    public Card Draw() => _cards.Pop();
}
