using BlackjackBot.Application.Interfaces;
using BlackjackBot.Domain.Entities;
using System.Collections.Concurrent;

namespace BlackjackBot.Application.Services;

public class GameSessionManager : IGameSessionManager
{
    private readonly ConcurrentDictionary<ulong, GameState> _games = new();
    private readonly ConcurrentDictionary<ulong, MinesweeperGameState> _minesGames = new();
    private readonly ConcurrentDictionary<ulong, HiloGameState> _hiloGames = new();

    public bool HasAnyActiveGame(ulong userId) =>
        _games.ContainsKey(userId) || _minesGames.ContainsKey(userId) || _hiloGames.ContainsKey(userId);

    public bool TryGetGame(ulong userId, out GameState? game) => _games.TryGetValue(userId, out game);
    public bool TryAddGame(GameState game) => _games.TryAdd(game.UserId, game);
    public void RemoveGame(ulong userId) => _games.TryRemove(userId, out _);

    public bool TryGetMinesGame(ulong userId, out MinesweeperGameState? game) => _minesGames.TryGetValue(userId, out game);
    public bool TryAddMinesGame(MinesweeperGameState game) => _minesGames.TryAdd(game.UserId, game);
    public void RemoveMinesGame(ulong userId) => _minesGames.TryRemove(userId, out _);

    public bool TryGetHiloGame(ulong userId, out HiloGameState? game) => _hiloGames.TryGetValue(userId, out game);
    public bool TryAddHiloGame(HiloGameState game) => _hiloGames.TryAdd(game.UserId, game);
    public void RemoveHiloGame(ulong userId) => _hiloGames.TryRemove(userId, out _);
}
