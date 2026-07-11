using BlackjackBot.Application.Interfaces;
using BlackjackBot.Domain.Entities;
using System.Collections.Concurrent;

namespace BlackjackBot.Application.Services;

public class GameSessionManager : IGameSessionManager
{
    private readonly ConcurrentDictionary<ulong, GameState> _games = new();
    private readonly ConcurrentDictionary<ulong, MinesweeperGameState> _minesGames = new();

    // Проверка, есть ли у игрока ЛЮБАЯ активная игра (чтобы нельзя было запустить краш во время сапёра)
    public bool HasAnyActiveGame(ulong userId) => _games.ContainsKey(userId) || _minesGames.ContainsKey(userId);

    public bool TryGetGame(ulong userId, out GameState? game) => _games.TryGetValue(userId, out game);
    public bool TryAddGame(GameState game) => _games.TryAdd(game.UserId, game);
    public void RemoveGame(ulong userId) => _games.TryRemove(userId, out _);

    public bool TryGetMinesGame(ulong userId, out MinesweeperGameState? game) => _minesGames.TryGetValue(userId, out game);
    public bool TryAddMinesGame(MinesweeperGameState game) => _minesGames.TryAdd(game.UserId, game);
    public void RemoveMinesGame(ulong userId) => _minesGames.TryRemove(userId, out _);
}
