using BlackjackBot.Domain.Entities;

namespace BlackjackBot.Domain.Interfaces;

public interface IPlayerRepository
{
    Task<Player> GetOrCreateAsync(ulong id);
    Task UpdateAsync(Player player);
    Task<List<Player>> GetTopPlayersAsync(int count = 10); // <-- Новое
}
