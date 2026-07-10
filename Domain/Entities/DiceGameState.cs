namespace BlackjackBot.Domain.Entities;

public class DiceGameState
{
    public long Id { get; set; }
    public ulong UserId { get; set; }
    public int Bet { get; set; }
    public int MinNumber { get; set; }
    public int MaxNumber { get; set; }
    public double Multiplier { get; set; }
    public int RolledNumber { get; set; }

    public bool IsWin => RolledNumber >= MinNumber && RolledNumber <= MaxNumber;
    public int Payout => IsWin ? (int)(Bet * Multiplier) : 0;

    public string ServerSeed { get; set; } = "";
    public string ServerSeedHash { get; set; } = "";
    public string ClientSeed { get; set; } = "";
}
