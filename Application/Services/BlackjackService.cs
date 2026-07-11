using BlackjackBot.Application.Interfaces;
using BlackjackBot.Domain.Common;
using BlackjackBot.Domain.Entities;
using BlackjackBot.Domain.Interfaces;

namespace BlackjackBot.Application.Services;

public class BlackjackService : IBlackjackService
{
    private readonly IPlayerRepository _playerRepo;
    private readonly IGameHistoryRepository _historyRepo;
    private readonly IGameSessionManager _sessionManager;

    public BlackjackService(IPlayerRepository playerRepo, IGameHistoryRepository historyRepo, IGameSessionManager sessionManager)
    {
        _playerRepo = playerRepo;
        _historyRepo = historyRepo;
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
        if (_sessionManager.HasAnyActiveGame(userId)) return Result<GameState>.Failure("Завершите текущую игру!");

        if (bet < 50) return Result<GameState>.Failure("Минимальная ставка - 50 монет.");
        var player = await _playerRepo.GetOrCreateAsync(userId);
        if (player.Balance < bet) return Result<GameState>.Failure($"Недостаточно средств. Баланс: {player.Balance}");

        // PROVABLY FAIR: Берем ЗАРАНЕЕ сгенерированный сид
        EnsureNextSeedExists(player);
        string serverSeed = player.NextServerSeed;
        string serverSeedHash = player.NextServerSeedHash;
        if (string.IsNullOrWhiteSpace(player.ClientSeed)) player.ClientSeed = "default_seed";

        var history = await _historyRepo.CreateAsync(new GameHistory
        {
            UserId = userId,
            ServerSeed = serverSeed,
            ClientSeed = player.ClientSeed,
            ServerSeedHash = serverSeedHash,
            IsCompleted = false,
            GameType = "Blackjack"
        });

        var game = new GameState(userId, bet, serverSeed, player.ClientSeed, history.Id)
        {
            Id = history.Id,
            ServerSeed = serverSeed,
            ServerSeedHash = serverSeedHash,
            ClientSeed = player.ClientSeed
        };

        if (!_sessionManager.TryAddGame(game)) return Result<GameState>.Failure("Завершите текущую игру!");

        // ВАЖНО: Только после успешного старта генерируем новый сид для СЛЕДУЮЩЕЙ игры
        player.NextServerSeed = Guid.NewGuid().ToString("N");
        player.NextServerSeedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(player.NextServerSeed))).ToLower();

        player.Balance -= bet;

        var hand = game.Hands[0];
        hand.Cards.Add(game.Deck.Draw());
        game.DealerHand.Add(game.Deck.Draw());
        hand.Cards.Add(game.Deck.Draw());
        game.DealerHand.Add(game.Deck.Draw());

        if (hand.Score == 21 || game.DealerScore == 21)
        {
            game.IsGameOver = true;
            player.BjGamesPlayed++; // <-- Изменено

            if (hand.Score == 21 && game.DealerScore == 21)
            {
                hand.Status = GameStatus.Push;
                player.Balance += bet;
                player.BjDraws++; // <-- Изменено
            }
            else if (hand.Score == 21)
            {
                hand.Status = GameStatus.BlackjackWin;
                player.Balance += (int)(bet * 2.5);
                player.BjWins++; // <-- Изменено
                player.Blackjacks++;
                player.BjTotalMoneyWon += (int)(bet * 1.5); // <-- Изменено
            }
            else
            {
                hand.Status = GameStatus.DealerWin;
                player.BjLosses++; // <-- Изменено
                player.BjTotalMoneyLost += bet; // <-- Изменено
            }

            await _playerRepo.UpdateAsync(player);
            _sessionManager.RemoveGame(userId);
            await _historyRepo.UpdateToCompletedAsync(game.Id);
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
                player.BjGamesPlayed++; // <-- Изменено
                player.BjLosses++; // <-- Изменено
                player.BjTotalMoneyLost += hand.Bet; // <-- Изменено
                continue;
            }

            if (game.DealerScore > 21) hand.Status = GameStatus.DealerBust;
            else if (game.DealerScore > hand.Score) hand.Status = GameStatus.DealerWin;
            else if (game.DealerScore < hand.Score) hand.Status = GameStatus.PlayerWin;
            else hand.Status = GameStatus.Push;

            player.BjGamesPlayed++; // <-- Изменено

            int payout = hand.Status switch
            {
                GameStatus.DealerBust or GameStatus.PlayerWin => hand.Bet * 2,
                GameStatus.Push => hand.Bet,
                _ => 0
            };

            int netProfit = payout - hand.Bet;

            if (hand.Status == GameStatus.Push) player.BjDraws++; // <-- Изменено
            else if (payout > 0)
            {
                player.BjWins++; // <-- Изменено
                player.BjTotalMoneyWon += netProfit; // <-- Изменено
            }
            else
            {
                player.BjLosses++; // <-- Изменено
                player.BjTotalMoneyLost += hand.Bet; // <-- Изменено
            }

            player.Balance += payout;
        }

        await _playerRepo.UpdateAsync(player);
        _sessionManager.RemoveGame(game.UserId);

        await _historyRepo.UpdateToCompletedAsync(game.Id); // <-- Важно! Разрешаем показывать ServerSeed

        return Result<GameState>.Success(game);
    }

    public async Task<Result> ChangeSeedAsync(ulong userId, string newSeed)
    {
        if (string.IsNullOrWhiteSpace(newSeed) || newSeed.Length > 20 || !newSeed.All(c => char.IsLetterOrDigit(c) || c == '_'))
            return Result.Failure("Сид должен быть от 1 до 20 символов и содержать только буквы, цифры и _.");

        var player = await _playerRepo.GetOrCreateAsync(userId);
        player.ClientSeed = newSeed;
        await _playerRepo.UpdateAsync(player);

        return Result.Success();
    }

    public async Task<Result<GameHistory>> GetGameProofAsync(long gameId)
    {
        var history = await _historyRepo.GetByIdAsync(gameId);
        if (history is null) return Result<GameHistory>.Failure("Игра с таким ID не найдена.");
        if (!history.IsCompleted) return Result<GameHistory>.Failure("Эта игра еще идет! Серверный сид будет раскрыт только после её завершения.");

        return Result<GameHistory>.Success(history);
    }

    public async Task<Result<CrashGameState>> PlayCrashAsync(ulong userId, int bet, double targetMultiplier)
    {
        if (_sessionManager.HasAnyActiveGame(userId)) return Result<CrashGameState>.Failure("Завершите текущую игру!");

        if (bet < 50) return Result<CrashGameState>.Failure("Минимальная ставка - 50 монет.");
        if (targetMultiplier < 1.01) return Result<CrashGameState>.Failure("Минимальный множитель - 1.01x.");

        var player = await _playerRepo.GetOrCreateAsync(userId);
        if (player.Balance < bet) return Result<CrashGameState>.Failure($"Недостаточно средств. Баланс: {player.Balance}");

        // PROVABLY FAIR: Берем ЗАРАНЕЕ сгенерированный сид
        EnsureNextSeedExists(player);
        string serverSeed = player.NextServerSeed;
        string serverSeedHash = player.NextServerSeedHash;
        if (string.IsNullOrWhiteSpace(player.ClientSeed)) player.ClientSeed = "default_seed";

        var history = await _historyRepo.CreateAsync(new GameHistory
        {
            UserId = userId,
            ServerSeed = serverSeed,
            ClientSeed = player.ClientSeed,
            ServerSeedHash = serverSeedHash,
            IsCompleted = true,
            GameType = "Crash"
        });

        // Сразу генерируем новый сид для СЛЕДУЮЩЕЙ игры
        player.NextServerSeed = Guid.NewGuid().ToString("N");
        player.NextServerSeedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(player.NextServerSeed))).ToLower();

        player.Balance -= bet;

        // Provably Fair алгоритм для Краша БЕЗ House Edge
        byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{serverSeed}:{player.ClientSeed}:{history.Id}"));
        string hex = Convert.ToHexString(hashBytes).ToLower()[..13];
        long h = Convert.ToInt64(hex, 16);
        long e = (long)Math.Pow(2, 52);

        // Больше нет 5% шанса моментального взрыва (h % 20 != 0).
        // Чистая математическая модель распределения множителей:
        double actualMultiplier = Math.Floor((100.0 * e) / (e - h)) / 100.0;
        if (actualMultiplier < 1.00) actualMultiplier = 1.00;

        var game = new CrashGameState
        {
            Id = history.Id,
            UserId = userId,
            Bet = bet,
            TargetMultiplier = targetMultiplier,
            ActualMultiplier = actualMultiplier,
            ServerSeed = serverSeed,
            ServerSeedHash = serverSeedHash,
            ClientSeed = player.ClientSeed
        };

        if (game.IsWin)
        {
            player.Balance += game.Payout;
            player.CrashWins++; // <-- Изменено
            player.CrashTotalMoneyWon += (game.Payout - game.Bet); // <-- Изменено
        }
        else
        {
            player.CrashLosses++; // <-- Изменено
            player.CrashTotalMoneyLost += game.Bet; // <-- Изменено
        }
        player.CrashGamesPlayed++; // <-- Изменено

        await _playerRepo.UpdateAsync(player);
        return Result<CrashGameState>.Success(game);
    }

    private void EnsureNextSeedExists(Player player)
    {
        if (string.IsNullOrEmpty(player.NextServerSeed))
        {
            player.NextServerSeed = Guid.NewGuid().ToString("N");
            player.NextServerSeedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(player.NextServerSeed))).ToLower();
        }
    }

    public async Task<Result<(string ServerSeedHash, string ClientSeed)>> GetNextSeedInfoAsync(ulong userId)
    {
        var player = await _playerRepo.GetOrCreateAsync(userId);
        EnsureNextSeedExists(player);
        if (string.IsNullOrWhiteSpace(player.ClientSeed)) player.ClientSeed = "default_seed";

        await _playerRepo.UpdateAsync(player);

        return Result<(string, string)>.Success((player.NextServerSeedHash, player.ClientSeed));
    }

    public async Task<Result<DiceGameState>> PlayDiceAsync(ulong userId, int bet, int min, int max)
    {
        if (_sessionManager.HasAnyActiveGame(userId)) return Result<DiceGameState>.Failure("Завершите текущую игру!");

        if (bet < 50) return Result<DiceGameState>.Failure("Минимальная ставка - 50 монет.");
        if (min < 1 || max > 100 || min > max) return Result<DiceGameState>.Failure("Диапазон должен быть от 1 до 100 (например: 1 50).");

        int chance = max - min + 1;
        if (chance > 95) return Result<DiceGameState>.Failure("Максимальный шанс выигрыша — 95%. Выберите меньший диапазон.");

        var player = await _playerRepo.GetOrCreateAsync(userId);
        if (player.Balance < bet) return Result<DiceGameState>.Failure($"Недостаточно средств. Баланс: {player.Balance}");

        EnsureNextSeedExists(player);
        string serverSeed = player.NextServerSeed;
        string serverSeedHash = player.NextServerSeedHash;
        if (string.IsNullOrWhiteSpace(player.ClientSeed)) player.ClientSeed = "default_seed";

        var history = await _historyRepo.CreateAsync(new GameHistory
        {
            UserId = userId,
            ServerSeed = serverSeed,
            ClientSeed = player.ClientSeed,
            ServerSeedHash = serverSeedHash,
            IsCompleted = true,
            GameType = "Dice" // Указываем тип игры
        });

        // Новый сид для следующей игры
        player.NextServerSeed = Guid.NewGuid().ToString("N");
        player.NextServerSeedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(player.NextServerSeed))).ToLower();

        player.Balance -= bet;

        // Provably Fair алгоритм: берем первые 8 символов хеша (4 байта), переводим в число и берем остаток от деления на 100
        byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{serverSeed}:{player.ClientSeed}:{history.Id}"));
        string hex = Convert.ToHexString(hashBytes).ToLower()[..8];
        long h = Convert.ToInt64(hex, 16);
        int rolledNumber = (int)(h % 100) + 1;

        // Считаем множитель (100.0 / шанс) с округлением до 2 знаков БЕЗ House Edge
        double multiplier = Math.Round(100.0 / chance, 2);

        var game = new DiceGameState
        {
            Id = history.Id,
            UserId = userId,
            Bet = bet,
            MinNumber = min,
            MaxNumber = max,
            Multiplier = multiplier,
            RolledNumber = rolledNumber,
            ServerSeed = serverSeed,
            ServerSeedHash = serverSeedHash,
            ClientSeed = player.ClientSeed
        };

        if (game.IsWin)
        {
            player.Balance += game.Payout;
            player.DiceWins++;
            player.DiceTotalMoneyWon += (game.Payout - game.Bet);
        }
        else
        {
            player.DiceLosses++;
            player.DiceTotalMoneyLost += game.Bet;
        }
        player.DiceGamesPlayed++;

        await _playerRepo.UpdateAsync(player);
        return Result<DiceGameState>.Success(game);
    }

    public async Task<Result<MinesweeperGameState>> StartMinesweeperAsync(ulong userId, int bet, int minesCount)
    {
        if (bet < 50) return Result<MinesweeperGameState>.Failure("Минимальная ставка - 50 монет.");
        if (minesCount < 1 || minesCount > 19) return Result<MinesweeperGameState>.Failure("Количество бомб должно быть от 1 до 19.");
        if (_sessionManager.HasAnyActiveGame(userId)) return Result<MinesweeperGameState>.Failure("Завершите текущую игру!");

        var player = await _playerRepo.GetOrCreateAsync(userId);
        if (player.Balance < bet) return Result<MinesweeperGameState>.Failure($"Недостаточно средств. Баланс: {player.Balance}");

        EnsureNextSeedExists(player);
        string serverSeed = player.NextServerSeed;
        string serverSeedHash = player.NextServerSeedHash;
        if (string.IsNullOrWhiteSpace(player.ClientSeed)) player.ClientSeed = "default_seed";

        var history = await _historyRepo.CreateAsync(new GameHistory
        {
            UserId = userId,
            ServerSeed = serverSeed,
            ClientSeed = player.ClientSeed,
            ServerSeedHash = serverSeedHash,
            IsCompleted = false,
            GameType = "Minesweeper"
        });

        player.NextServerSeed = Guid.NewGuid().ToString("N");
        player.NextServerSeedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(player.NextServerSeed))).ToLower();

        player.Balance -= bet;
        await _playerRepo.UpdateAsync(player);

        // Provably Fair алгоритм генерации бомб
        var tilesWithHashes = Enumerable.Range(0, 20).Select(index =>
        {
            var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{serverSeed}:{player.ClientSeed}:{history.Id}:{index}"));
            return new { Index = index, Hash = Convert.ToHexString(hashBytes).ToLower() };
        }).ToList();

        // Сортируем плитки по хешу и берем первые `minesCount` индексов
        var minePositions = tilesWithHashes.OrderBy(x => x.Hash).Take(minesCount).Select(x => x.Index).ToList();

        var game = new MinesweeperGameState
        {
            Id = history.Id,
            UserId = userId,
            Bet = bet,
            MinesCount = minesCount,
            MinePositions = minePositions,
            ServerSeed = serverSeed,
            ServerSeedHash = serverSeedHash,
            ClientSeed = player.ClientSeed
        };

        _sessionManager.TryAddMinesGame(game);
        return Result<MinesweeperGameState>.Success(game);
    }

    public async Task<Result<MinesweeperGameState>> ClickMinesweeperAsync(ulong userId, int tileIndex)
    {
        if (!_sessionManager.TryGetMinesGame(userId, out var game) || game is null)
            return Result<MinesweeperGameState>.Failure("Игра не найдена.");

        if (game.RevealedPositions.Contains(tileIndex))
            return Result<MinesweeperGameState>.Failure("Эта плитка уже открыта!");

        var player = await _playerRepo.GetOrCreateAsync(userId);

        if (game.MinePositions.Contains(tileIndex))
        {
            // БАБАХ!
            game.IsGameOver = true;
            game.IsBusted = true;
            game.BustedOnTile = tileIndex;

            player.MinesGamesPlayed++;
            player.MinesLosses++;
            player.MinesTotalMoneyLost += game.Bet;

            await _playerRepo.UpdateAsync(player);
            _sessionManager.RemoveMinesGame(userId);
            await _historyRepo.UpdateToCompletedAsync(game.Id);

            return Result<MinesweeperGameState>.Success(game);
        }

        // Успешно открыли безопасную плитку
        game.RevealedPositions.Add(tileIndex);

        // Проверка: открыты ли ВСЕ безопасные плитки?
        if (game.RevealedPositions.Count == 20 - game.MinesCount)
        {
            return await CashoutMinesweeperAsync(userId); // Автоматический вывод
        }

        return Result<MinesweeperGameState>.Success(game);
    }

    public async Task<Result<MinesweeperGameState>> CashoutMinesweeperAsync(ulong userId)
    {
        if (!_sessionManager.TryGetMinesGame(userId, out var game) || game is null)
            return Result<MinesweeperGameState>.Failure("Игра не найдена.");

        if (game.RevealedPositions.Count == 0)
            return Result<MinesweeperGameState>.Failure("Сначала откройте хотя бы одну плитку!");

        game.IsGameOver = true;
        game.IsCashedOut = true;

        var player = await _playerRepo.GetOrCreateAsync(userId);

        player.Balance += game.CurrentPayout;
        player.MinesGamesPlayed++;
        player.MinesWins++;
        player.MinesTotalMoneyWon += (game.CurrentPayout - game.Bet);

        await _playerRepo.UpdateAsync(player);
        _sessionManager.RemoveMinesGame(userId);
        await _historyRepo.UpdateToCompletedAsync(game.Id);

        return Result<MinesweeperGameState>.Success(game);
    }
}
