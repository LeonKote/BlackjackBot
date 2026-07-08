namespace BlackjackBot.Domain.Entities;

public class GameState
{
    public ulong UserId { get; }
    public Deck Deck { get; set; } = new();
    public List<Card> DealerHand { get; set; } = [];

    // Поддержка нескольких рук для Сплита
    public List<Hand> Hands { get; set; } = [];
    public int CurrentHandIndex { get; set; } = 0;

    public Hand CurrentHand => Hands[CurrentHandIndex];
    public bool IsGameOver { get; set; } = false;

    public int DealerScore => CalculateScore(DealerHand);

    public GameState(ulong userId, int initialBet)
    {
        UserId = userId;
        Hands.Add(new Hand { Bet = initialBet });
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
