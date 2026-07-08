namespace BlackjackBot.Domain.Entities;

public class Hand
{
    public List<Card> Cards { get; set; } = [];
    public int Bet { get; set; }
    public GameStatus Status { get; set; } = GameStatus.Active;
    public int Score => GameState.CalculateScore(Cards);
}
