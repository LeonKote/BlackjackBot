namespace BlackjackBot.Domain.Entities;

public class GameHistory
{
    public long Id { get; set; }
    public ulong UserId { get; set; }
    public string ServerSeed { get; set; } = "";
    public string ClientSeed { get; set; } = "";
    public string ServerSeedHash { get; set; } = "";
    public bool IsCompleted { get; set; } = false;

    // Новое поле для определения игры (Blackjack или Crash)
    public string GameType { get; set; } = "Blackjack";
}
