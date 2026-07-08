using BlackjackBot.Domain.Common;
using BlackjackBot.Domain.Entities;

namespace BlackjackBot.Application.Interfaces;

public interface IBlackjackService
{
    Task<Result<(int Balance, DateTimeOffset NextAvailable)>> ClaimHourlyAsync(ulong userId);
    Task<Result<GameState>> StartGameAsync(ulong userId, int bet);
    Task<Result<GameState>> HitAsync(ulong userId);
    Task<Result<GameState>> StandAsync(ulong userId);
    Task<Result<GameState>> DoubleDownAsync(ulong userId); // <-- Дабл
    Task<Result<GameState>> SplitAsync(ulong userId);      // <-- Сплит
}
