namespace BlackjackBot.Domain.Entities;

public class CrashGameState
{
    public long Id { get; set; }
    public ulong UserId { get; set; }
    public int Bet { get; set; }
    public double TargetMultiplier { get; set; }
    public double ActualMultiplier { get; set; }

    public bool IsWin => ActualMultiplier >= TargetMultiplier;
    public int Payout => IsWin ? (int)(Bet * TargetMultiplier) : 0;

    public string ServerSeed { get; set; } = "";
    public string ServerSeedHash { get; set; } = "";
    public string ClientSeed { get; set; } = "";
}
