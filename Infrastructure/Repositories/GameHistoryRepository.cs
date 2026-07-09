using BlackjackBot.Domain.Entities;
using BlackjackBot.Domain.Interfaces;
using BlackjackBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BlackjackBot.Infrastructure.Repositories;

public class GameHistoryRepository : IGameHistoryRepository
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public GameHistoryRepository(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<GameHistory> CreateAsync(GameHistory history)
    {
        using var db = _dbFactory.CreateDbContext();
        db.GameHistories.Add(history);
        await db.SaveChangesAsync();
        return history;
    }

    public async Task UpdateToCompletedAsync(long id)
    {
        using var db = _dbFactory.CreateDbContext();
        var history = await db.GameHistories.FindAsync(id);
        if (history != null)
        {
            history.IsCompleted = true;
            await db.SaveChangesAsync();
        }
    }

    public async Task<GameHistory?> GetByIdAsync(long id)
    {
        using var db = _dbFactory.CreateDbContext();
        return await db.GameHistories.FindAsync(id);
    }
}
