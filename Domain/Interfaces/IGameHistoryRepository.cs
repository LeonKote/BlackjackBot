using BlackjackBot.Domain.Entities;

namespace BlackjackBot.Domain.Interfaces;

public interface IGameHistoryRepository
{
    Task<GameHistory> CreateAsync(GameHistory history); // Возвращает сохраненный с ID
    Task UpdateToCompletedAsync(long id); // Отмечаем завершение
    Task<GameHistory?> GetByIdAsync(long id); // Поиск по ID
}
