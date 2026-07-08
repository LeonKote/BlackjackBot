using BlackjackBot.Domain.Entities;

namespace BlackjackBot.Application.Interfaces;

public interface IGameSessionManager
{
    bool TryGetGame(ulong userId, out GameState? game);
    bool TryAddGame(GameState game); // <-- Новое
    void RemoveGame(ulong userId);
    bool HasGame(ulong userId);
}
