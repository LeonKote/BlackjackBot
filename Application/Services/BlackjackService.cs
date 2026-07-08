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
            return Result<(int, DateTimeOffset)>.Failure("Время еще не пришло", (player.Balance, player.LastHourly.AddHours(1)));
        }

        player.Balance += 1000;
        player.LastHourly = DateTimeOffset.UtcNow;
        await _playerRepo.UpdateAsync(player);

        return Result<(int, DateTimeOffset)>.Success((player.Balance, default));
    }

    public async Task<Result<GameState>> StartGameAsync(ulong userId, int bet)
    {
        if (bet < 50) return Result<GameState>.Failure("Минимальная ставка - 50 монет.");

        var game = new GameState(userId, bet);
        if (!_sessionManager.TryAddGame(game))
            return Result<GameState>.Failure("Завершите текущую игру!");

        var player = await _playerRepo.GetOrCreateAsync(userId);
        if (player.Balance < bet)
        {
            _sessionManager.RemoveGame(userId);
            return Result<GameState>.Failure($"Недостаточно средств. Баланс: {player.Balance}");
        }

        player.Balance -= bet;

        var hand = game.Hands[0];
        hand.Cards.Add(game.Deck.Draw());
        game.DealerHand.Add(game.Deck.Draw());
        hand.Cards.Add(game.Deck.Draw());
        game.DealerHand.Add(game.Deck.Draw());

        if (hand.Score == 21 || game.DealerScore == 21)
        {
            game.IsGameOver = true;
            player.GamesPlayed++;

            if (hand.Score == 21 && game.DealerScore == 21) { hand.Status = GameStatus.Push; player.Balance += bet; player.Draws++; }
            else if (hand.Score == 21) { hand.Status = GameStatus.BlackjackWin; player.Balance += (int)(bet * 2.5); player.Wins++; player.Blackjacks++; }
            else { hand.Status = GameStatus.DealerWin; player.Losses++; }

            await _playerRepo.UpdateAsync(player);
            _sessionManager.RemoveGame(userId);
            return Result<GameState>.Success(game);
        }

        await _playerRepo.UpdateAsync(player);
        return Result<GameState>.Success(game);
    }

    public async Task<Result<GameState>> HitAsync(ulong userId)
    {
        if (!_sessionManager.TryGetGame(userId, out var game) || game is null)
            return Result<GameState>.Failure("Игра не найдена.");

        var hand = game.CurrentHand;
        hand.Cards.Add(game.Deck.Draw());

        if (hand.Score > 21)
        {
            hand.Status = GameStatus.PlayerBust;
            return await NextHandOrFinishAsync(game);
        }

        if (hand.Score == 21)
            return await NextHandOrFinishAsync(game);

        return Result<GameState>.Success(game);
    }

    public async Task<Result<GameState>> StandAsync(ulong userId)
    {
        if (!_sessionManager.TryGetGame(userId, out var game) || game is null)
            return Result<GameState>.Failure("Игра не найдена.");

        return await NextHandOrFinishAsync(game);
    }

    public async Task<Result<GameState>> DoubleDownAsync(ulong userId)
    {
        if (!_sessionManager.TryGetGame(userId, out var game) || game is null)
            return Result<GameState>.Failure("Игра не найдена.");

        var hand = game.CurrentHand;
        if (hand.Cards.Count != 2) return Result<GameState>.Failure("Дабл возможен только первым ходом!");

        var player = await _playerRepo.GetOrCreateAsync(userId);
        if (player.Balance < hand.Bet) return Result<GameState>.Failure("Недостаточно средств для дабла!");

        player.Balance -= hand.Bet;
        await _playerRepo.UpdateAsync(player);

        hand.Bet *= 2;
        hand.Cards.Add(game.Deck.Draw()); // При дабле берется только ОДНА карта

        if (hand.Score > 21) hand.Status = GameStatus.PlayerBust;

        return await NextHandOrFinishAsync(game);
    }

    public async Task<Result<GameState>> SplitAsync(ulong userId)
    {
        if (!_sessionManager.TryGetGame(userId, out var game) || game is null)
            return Result<GameState>.Failure("Игра не найдена.");

        var hand = game.CurrentHand;
        if (hand.Cards.Count != 2 || hand.Cards[0].Rank != hand.Cards[1].Rank)
            return Result<GameState>.Failure("Сплит возможен только при двух картах одинакового достоинства!");

        if (game.Hands.Count >= 4)
            return Result<GameState>.Failure("Максимальное количество рук (4) достигнуто!");

        var player = await _playerRepo.GetOrCreateAsync(userId);
        if (player.Balance < hand.Bet) return Result<GameState>.Failure("Недостаточно средств для сплита!");

        player.Balance -= hand.Bet;
        await _playerRepo.UpdateAsync(player);

        // Разделяем карты
        var splitCard = hand.Cards[1];
        hand.Cards.RemoveAt(1);

        var newHand = new Hand { Bet = hand.Bet };
        newHand.Cards.Add(splitCard);
        game.Hands.Insert(game.CurrentHandIndex + 1, newHand);

        // Докидываем карту первой руке
        hand.Cards.Add(game.Deck.Draw());

        // Если при доборе получилось 21, авто-стенд и переход к следующей руке
        if (hand.Score == 21) return await NextHandOrFinishAsync(game);

        return Result<GameState>.Success(game);
    }

    // Вспомогательный метод перехода к следующей руке (если был сплит) или завершения игры
    private async Task<Result<GameState>> NextHandOrFinishAsync(GameState game)
    {
        game.CurrentHandIndex++;

        if (game.CurrentHandIndex < game.Hands.Count)
        {
            var hand = game.CurrentHand;
            if (hand.Cards.Count == 1) // Если мы перешли на сплит-руку, докидываем ей карту
            {
                hand.Cards.Add(game.Deck.Draw());
                if (hand.Score == 21)
                    return await NextHandOrFinishAsync(game);
            }
            return Result<GameState>.Success(game);
        }

        return await FinishGameAsync(game);
    }

    private async Task<Result<GameState>> FinishGameAsync(GameState game)
    {
        game.IsGameOver = true;

        bool allBusted = game.Hands.All(h => h.Status == GameStatus.PlayerBust);
        if (!allBusted)
        {
            while (game.DealerScore < 17) game.DealerHand.Add(game.Deck.Draw());
        }

        var player = await _playerRepo.GetOrCreateAsync(game.UserId);

        foreach (var hand in game.Hands)
        {
            if (hand.Status == GameStatus.PlayerBust)
            {
                player.GamesPlayed++;
                player.Losses++;
                continue;
            }

            if (game.DealerScore > 21) hand.Status = GameStatus.DealerBust;
            else if (game.DealerScore > hand.Score) hand.Status = GameStatus.DealerWin;
            else if (game.DealerScore < hand.Score) hand.Status = GameStatus.PlayerWin;
            else hand.Status = GameStatus.Push;

            player.GamesPlayed++;

            int payout = hand.Status switch
            {
                GameStatus.DealerBust or GameStatus.PlayerWin => hand.Bet * 2,
                GameStatus.Push => hand.Bet,
                _ => 0
            };

            if (hand.Status == GameStatus.Push) player.Draws++;
            else if (payout > 0) player.Wins++;
            else player.Losses++;

            player.Balance += payout;
        }

        await _playerRepo.UpdateAsync(player);
        _sessionManager.RemoveGame(game.UserId);

        return Result<GameState>.Success(game);
    }
}
