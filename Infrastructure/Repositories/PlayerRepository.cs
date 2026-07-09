using BlackjackBot.Domain.Entities;
using BlackjackBot.Domain.Interfaces;
using BlackjackBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BlackjackBot.Infrastructure.Repositories;

public class PlayerRepository : IPlayerRepository
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public PlayerRepository(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<Player> GetOrCreateAsync(ulong id)
    {
        using var db = _dbFactory.CreateDbContext();
        var player = await db.Players.FindAsync(id);
        if (player is null)
        {
            player = new Player { Id = id, Balance = 2500, LastHourly = DateTimeOffset.MinValue };
            db.Players.Add(player);
            await db.SaveChangesAsync();
        }
        return player;
    }

    public async Task UpdateAsync(Player player)
    {
        using var db = _dbFactory.CreateDbContext();
        db.Players.Update(player);
        await db.SaveChangesAsync();
    }
}
