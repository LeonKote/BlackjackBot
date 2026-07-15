namespace BlackjackBot.Domain.Entities;

public class GameState
{
    public long Id { get; set; }
    public ulong UserId { get; }
    public Deck Deck { get; set; }
    public List<Card> DealerHand { get; set; } = [];

    public List<Hand> Hands { get; set; } = [];
    public int CurrentHandIndex { get; set; } = 0;

    public Hand CurrentHand => Hands[CurrentHandIndex];
    public bool IsGameOver { get; set; } = false;

    public string ServerSeed { get; set; } = "";
    public string ServerSeedHash { get; set; } = "";
    public string ClientSeed { get; set; } = "";

    public bool IsBoosted { get; set; }
    public bool IsMegaBoosted { get; set; }

    public bool HasPeeked { get; set; } = false; // <-- Добавили

    public int DealerScore => CalculateScore(DealerHand);

    public GameState(ulong userId, int initialBet, string serverSeed, string clientSeed, long gameId)
    {
        UserId = userId;
        Hands.Add(new Hand { Bet = initialBet });
        Deck = new Deck(serverSeed, clientSeed, gameId); // Передаем gameId в колоду
    }

    public static int CalculateScore(IEnumerable<Card> hand)
    {
        int score = 0, aces = 0;
        foreach (var card in hand)
        {
            if (card.Rank == Rank.Ace) { score += 11; aces++; }
            else if (card.Rank is Rank.Jack or Rank.Queen or Rank.King) score += 10;
            else score += (int)card.Rank;
        }
        while (score > 21 && aces > 0) { score -= 10; aces--; }
        return score;
    }
}
