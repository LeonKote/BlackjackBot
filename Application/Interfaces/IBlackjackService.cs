using BlackjackBot.Domain.Common;
using BlackjackBot.Domain.Entities;

namespace BlackjackBot.Application.Interfaces;

public interface IBlackjackService
{
    Task<Result<(int Balance, DateTimeOffset NextAvailable)>> ClaimHourlyAsync(ulong userId);
    Task<Result<GameState>> StartGameAsync(ulong userId, int bet);
    Task<Result<GameState>> HitAsync(ulong userId);
    Task<Result<GameState>> StandAsync(ulong userId);
    Task<Result<GameState>> DoubleDownAsync(ulong userId);
    Task<Result<GameState>> SplitAsync(ulong userId);

    // Новые методы
    Task<Result> ChangeSeedAsync(ulong userId, string newSeed);
    Task<Result<GameHistory>> GetGameProofAsync(long gameId); // ID теперь long
    Task<Result<CrashGameState>> PlayCrashAsync(ulong userId, int bet, double targetMultiplier);
    Task<Result<(string ServerSeedHash, string ClientSeed)>> GetNextSeedInfoAsync(ulong userId);
    Task<Result<DiceGameState>> PlayDiceAsync(ulong userId, int bet, int min, int max);

    Task<Result<MinesweeperGameState>> StartMinesweeperAsync(ulong userId, int bet, int minesCount);
    Task<Result<MinesweeperGameState>> ClickMinesweeperAsync(ulong userId, int tileIndex);
    Task<Result<MinesweeperGameState>> CashoutMinesweeperAsync(ulong userId);

    Task<Result<HiloGameState>> StartHiloAsync(ulong userId, int bet);
    Task<Result<HiloGameState>> GuessHiloAsync(ulong userId, string guess);
    Task<Result<HiloGameState>> CashoutHiloAsync(ulong userId);

    Task<Result<(int Balance, DateTimeOffset NextAvailable)>> ClaimDailyAsync(ulong userId);
    Task<List<Player>> GetTopPlayersAsync(int count = 10);

    Task<Result<int>> PreVipCheckAsync(ulong userId);
    Task<Result> ConfirmVipAsync(ulong userId);

    Task<Result<int>> PreBoosterCheckAsync(ulong userId, bool isMega);
    Task<Result> ConfirmBoosterAsync(ulong userId, bool isMega);

    Task<Result<int>> PrePeekCheckAsync(ulong userId);
    Task<Result<string>> ConfirmPeekAsync(ulong userId);

    Task<Result<int>> PreRefundCheckAsync(ulong userId);
    Task<Result> ConfirmRefundAsync(ulong userId);
}
