namespace BlackjackBot.Domain.Entities;

public class GameHistory
{
    public long Id { get; set; }
    public ulong UserId { get; set; }
    public string ServerSeed { get; set; } = "";
    public string ClientSeed { get; set; } = "";
    // Убрали Nonce
    public string ServerSeedHash { get; set; } = "";
    public bool IsCompleted { get; set; } = false;
}
