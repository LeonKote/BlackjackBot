using BlackjackBot.Application.Interfaces;
using BlackjackBot.Domain.Common;
using BlackjackBot.Domain.Entities;
using BlackjackBot.Domain.Interfaces;

namespace BlackjackBot.Application.Services;

public class BlackjackService : IBlackjackService
{
    private readonly IPlayerRepository _playerRepo;
    private readonly IGameSessionManager _sessionManager;

    public BlackjackService(IPlayerRepository playerRepo, IGameSessionManager sessionManager)
    {
        _playerRepo = playerRepo;
        _sessionManager = sessionManager;
    }

    public async Task<Result<(int Balance, DateTimeOffset NextAvailable)>> ClaimHourlyAsync(ulong userId)
    {
        var player = await _playerRepo.GetOrCreateAsync(userId);

        if ((DateTimeOffset.UtcNow - player.LastHourly).TotalHours < 1)
        {
            // Высчитываем, когда можно будет взять бонус
            var nextAvailable = player.LastHourly.AddHours(1);

            // Передаем это время в Failure
            return Result<(int, DateTimeOffset)>.Failure("Время еще не пришло", (player.Balance, nextAvailable));
        }

        player.Balance += 1000;
        player.LastHourly = DateTimeOffset.UtcNow;
        await _playerRepo.UpdateAsync(player);

        return Result<(int, DateTimeOffset)>.Success((player.Balance, default));
    }

    public async Task<Result<GameState>> StartGameAsync(ulong userId, int bet)
    {
        if (bet < 50) return Result<GameState>.Failure("Минимальная ставка - 50 монет.");
        if (_sessionManager.HasGame(userId)) return Result<GameState>.Failure("Завершите текущую игру!");

        var player = await _playerRepo.GetOrCreateAsync(userId);
        if (player.Balance < bet) return Result<GameState>.Failure($"Недостаточно средств. Баланс: {player.Balance}");

        player.Balance -= bet;

        var game = new GameState(userId, bet);
        game.PlayerHand.Add(game.Deck.Draw());
        game.DealerHand.Add(game.Deck.Draw());
        game.PlayerHand.Add(game.Deck.Draw());
        game.DealerHand.Add(game.Deck.Draw());

        // Моментальный исход при раздаче
        if (game.PlayerScore == 21 || game.DealerScore == 21)
        {
            player.GamesPlayed++;

            if (game.PlayerScore == 21 && game.DealerScore == 21) { game.Status = GameStatus.Push; player.Balance += bet; player.Draws++; }
            else if (game.PlayerScore == 21) { game.Status = GameStatus.BlackjackWin; player.Balance += (int)(bet * 2.5); player.Wins++; player.Blackjacks++; }
            else { game.Status = GameStatus.DealerWin; player.Losses++; }

            await _playerRepo.UpdateAsync(player);
            return Result<GameState>.Success(game);
        }

        await _playerRepo.UpdateAsync(player); // Сохраняем списание баланса
        _sessionManager.AddGame(game);
        return Result<GameState>.Success(game);
    }

    public async Task<Result<GameState>> HitAsync(ulong userId)
    {
        if (!_sessionManager.TryGetGame(userId, out var game) || game is null)
            return Result<GameState>.Failure("Игра не найдена.");

        game.PlayerHand.Add(game.Deck.Draw());

        if (game.PlayerScore > 21)
        {
            game.Status = GameStatus.PlayerBust;
            _sessionManager.RemoveGame(userId);

            var player = await _playerRepo.GetOrCreateAsync(userId);
            player.GamesPlayed++;
            player.Losses++;
            await _playerRepo.UpdateAsync(player);

            return Result<GameState>.Success(game);
        }
        if (game.PlayerScore == 21) return await StandAsync(userId);

        return Result<GameState>.Success(game);
    }

    public async Task<Result<GameState>> StandAsync(ulong userId)
    {
        if (!_sessionManager.TryGetGame(userId, out var game) || game is null)
            return Result<GameState>.Failure("Игра не найдена.");

        while (game.DealerScore < 17) game.DealerHand.Add(game.Deck.Draw());

        if (game.DealerScore > 21) game.Status = GameStatus.DealerBust;
        else if (game.DealerScore > game.PlayerScore) game.Status = GameStatus.DealerWin;
        else if (game.DealerScore < game.PlayerScore) game.Status = GameStatus.PlayerWin;
        else game.Status = GameStatus.Push;

        _sessionManager.RemoveGame(userId);

        var player = await _playerRepo.GetOrCreateAsync(userId);
        player.GamesPlayed++;

        int payout = game.Status switch
        {
            GameStatus.DealerBust or GameStatus.PlayerWin => game.Bet * 2,
            GameStatus.Push => game.Bet,
            _ => 0
        };

        // Начисляем статистику
        if (game.Status == GameStatus.Push) player.Draws++;
        else if (payout > 0) player.Wins++;
        else player.Losses++;

        if (payout > 0)
            player.Balance += payout;

        await _playerRepo.UpdateAsync(player);

        return Result<GameState>.Success(game);
    }
}
