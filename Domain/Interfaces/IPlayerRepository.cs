using BlackjackBot.Domain.Entities;

namespace BlackjackBot.Domain.Interfaces;

public interface IPlayerRepository
{
    Task<Player> GetOrCreateAsync(ulong id);
    Task UpdateAsync(Player player);
}
