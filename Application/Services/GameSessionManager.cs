using BlackjackBot.Application.Interfaces;
using BlackjackBot.Domain.Entities;
using System.Collections.Concurrent;

namespace BlackjackBot.Application.Services;

public class GameSessionManager : IGameSessionManager
{
    private readonly ConcurrentDictionary<ulong, GameState> _games = new();
    public bool TryGetGame(ulong userId, out GameState? game) => _games.TryGetValue(userId, out game);
    public bool TryAddGame(GameState game) => _games.TryAdd(game.UserId, game); // <-- Реализация
    public void RemoveGame(ulong userId) => _games.TryRemove(userId, out _);
    public bool HasGame(ulong userId) => _games.ContainsKey(userId);
}
