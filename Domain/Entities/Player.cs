namespace BlackjackBot.Domain.Entities;

public class Player
{
    public ulong Id { get; set; }
    public int Balance { get; set; }
    public DateTimeOffset LastHourly { get; set; }
    public DateTimeOffset LastDaily { get; set; }

    public string ClientSeed { get; set; } = "default_seed";
    public string NextServerSeed { get; set; } = "";
    public string NextServerSeedHash { get; set; } = "";

    // 💎 ПРЕМИУМ И ЭКОНОМИКА
    public int Diamonds { get; set; }
    public DateTimeOffset VipUntil { get; set; }
    public bool HasActiveBooster { get; set; }
    public bool HasActiveMegaBooster { get; set; } // <-- НОВОЕ ПОЛЕ
    public bool IsVip => DateTimeOffset.UtcNow < VipUntil;

    // ⏳ ОТМЕНА ПРОИГРЫША
    public long LastLostBet { get; set; }
    public DateTimeOffset LastLossTime { get; set; }
    public bool IsLastLossRewinded { get; set; }

    // (Остальная статистика без изменений: BjGamesPlayed, CrashGamesPlayed и т.д.)
    public int BjGamesPlayed { get; set; }
    public int BjWins { get; set; }
    public int BjLosses { get; set; }
    public int BjDraws { get; set; }
    public int Blackjacks { get; set; }
    public long BjTotalMoneyWon { get; set; }
    public long BjTotalMoneyLost { get; set; }
    public int CrashGamesPlayed { get; set; }
    public int CrashWins { get; set; }
    public int CrashLosses { get; set; }
    public long CrashTotalMoneyWon { get; set; }
    public long CrashTotalMoneyLost { get; set; }
    public int DiceGamesPlayed { get; set; }
    public int DiceWins { get; set; }
    public int DiceLosses { get; set; }
    public long DiceTotalMoneyWon { get; set; }
    public long DiceTotalMoneyLost { get; set; }
    public int MinesGamesPlayed { get; set; }
    public int MinesWins { get; set; }
    public int MinesLosses { get; set; }
    public long MinesTotalMoneyWon { get; set; }
    public long MinesTotalMoneyLost { get; set; }
    public int HiloGamesPlayed { get; set; }
    public int HiloWins { get; set; }
    public int HiloLosses { get; set; }
    public long HiloTotalMoneyWon { get; set; }
    public long HiloTotalMoneyLost { get; set; }
}
