namespace BlackjackBot.Domain.Entities;

public class GameState
{
    public ulong UserId { get; }
    public int Bet { get; }
    public List<Card> PlayerHand { get; set; } = [];
    public List<Card> DealerHand { get; set; } = [];
    public Deck Deck { get; set; } = new();
    public GameStatus Status { get; set; } = GameStatus.Active;

    public int PlayerScore => CalculateScore(PlayerHand);
    public int DealerScore => CalculateScore(DealerHand);

    public GameState(ulong userId, int bet)
    {
        UserId = userId;
        Bet = bet;
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
