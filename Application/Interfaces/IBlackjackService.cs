using BlackjackBot.Domain.Common;
using BlackjackBot.Domain.Entities;

namespace BlackjackBot.Application.Interfaces;

public interface IBlackjackService
{
    public Task<Result<(int Balance, DateTimeOffset NextAvailable)>> ClaimHourlyAsync(ulong userId);
    public Task<Result<GameState>> StartGameAsync(ulong userId, int bet);
    public Task<Result<GameState>> HitAsync(ulong userId);
    public Task<Result<GameState>> StandAsync(ulong userId);
}
