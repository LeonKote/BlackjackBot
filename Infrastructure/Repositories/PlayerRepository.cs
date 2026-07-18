using BlackjackBot.Domain.Entities;
using BlackjackBot.Domain.Interfaces;
using BlackjackBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BlackjackBot.Infrastructure.Repositories;

public class PlayerRepository : IPlayerRepository
{
    private readonly AppDbContext _dbContext;

    public PlayerRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Player> GetOrCreateAsync(ulong id)
    {
        var player = await _dbContext.Players.FindAsync(id);
        if (player is null)
        {
            player = new Player { Id = id, Balance = 2500, LastHourly = DateTimeOffset.MinValue };
            _dbContext.Players.Add(player);
            await _dbContext.SaveChangesAsync();
        }
        return player;
    }

    public async Task UpdateAsync(Player player)
    {
        _dbContext.Players.Update(player);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<List<Player>> GetTopPlayersAsync(int count = 10)
    {
        // Сортируем по убыванию баланса и берем первые N записей
        return await _dbContext.Players
            .OrderByDescending(p => p.Balance)
            .Take(count)
            .ToListAsync();
    }
}
