using BlackjackBot.Domain.Entities;

namespace BlackjackBot.Application.Interfaces;

public interface IGameSessionManager
{
    bool TryGetGame(ulong userId, out GameState? game);
    void AddGame(GameState game);
    void RemoveGame(ulong userId);
    bool HasGame(ulong userId);
}
