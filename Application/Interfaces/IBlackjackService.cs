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
}
