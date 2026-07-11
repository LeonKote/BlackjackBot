namespace BlackjackBot.Domain.Entities;

public class MinesweeperGameState
{
    public long Id { get; set; }
    public ulong UserId { get; set; }
    public int Bet { get; set; }
    public int MinesCount { get; set; }

    public List<int> MinePositions { get; set; } = [];
    public HashSet<int> RevealedPositions { get; set; } = [];

    public bool IsGameOver { get; set; }
    public bool IsBusted { get; set; }
    public bool IsCashedOut { get; set; }

    public int BustedOnTile { get; set; } = -1;

    public string ServerSeed { get; set; } = "";
    public string ServerSeedHash { get; set; } = "";
    public string ClientSeed { get; set; } = "";

    public double CurrentMultiplier => CalculateMultiplier(RevealedPositions.Count, MinesCount);
    public int CurrentPayout => (int)(Bet * CurrentMultiplier);

    // Математическая формула множителя Сапёра (с 1% House Edge)
    public static double CalculateMultiplier(int revealed, int mines)
    {
        if (revealed == 0) return 1.0;
        double prob = 1.0;
        int remainingSafe = 20 - mines;
        int remainingTotal = 20;

        for (int i = 0; i < revealed; i++)
        {
            prob *= (double)remainingSafe / remainingTotal;
            remainingSafe--;
            remainingTotal--;
        }

        return Math.Round(0.99 / prob, 2);
    }
}
