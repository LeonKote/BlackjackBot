namespace BlackjackBot.Domain.Entities;

public class Player
{
    public ulong Id { get; set; }
    public int Balance { get; set; }
    public DateTimeOffset LastHourly { get; set; }

    public int GamesPlayed { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
    public int Blackjacks { get; set; }

    public string ClientSeed { get; set; } = "default_seed";

    // НОВЫЕ ПОЛЯ
    public long TotalMoneyWon { get; set; }
    public long TotalMoneyLost { get; set; }
}
