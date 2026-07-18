using BlackjackBot.Domain.Entities;
using BlackjackBot.Domain.Interfaces;
using BlackjackBot.Infrastructure.Data;

namespace BlackjackBot.Infrastructure.Repositories;

public class GameHistoryRepository : IGameHistoryRepository
{
    private readonly AppDbContext _dbContext;

    public GameHistoryRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GameHistory> CreateAsync(GameHistory history)
    {
        _dbContext.GameHistories.Add(history);
        await _dbContext.SaveChangesAsync();
        return history;
    }

    public async Task UpdateToCompletedAsync(long id)
    {
        var history = await _dbContext.GameHistories.FindAsync(id);
        if (history != null)
        {
            history.IsCompleted = true;
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task<GameHistory?> GetByIdAsync(long id)
    {
        return await _dbContext.GameHistories.FindAsync(id);
    }
}
