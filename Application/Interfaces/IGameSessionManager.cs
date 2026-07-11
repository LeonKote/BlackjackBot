using BlackjackBot.Domain.Entities;

namespace BlackjackBot.Application.Interfaces;

public interface IGameSessionManager
{
    bool HasAnyActiveGame(ulong userId); // <-- Новое

    // Блекджек
    bool TryGetGame(ulong userId, out GameState? game);
    bool TryAddGame(GameState game);
    void RemoveGame(ulong userId);

    // Сапёр
    bool TryGetMinesGame(ulong userId, out MinesweeperGameState? game);
    bool TryAddMinesGame(MinesweeperGameState game);
    void RemoveMinesGame(ulong userId);
}
