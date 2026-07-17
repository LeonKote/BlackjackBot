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

        // VIP-игроки могут брать бонус каждые 30 минут (0.5 часа), обычные - раз в час (1.0)
        double hoursNeeded = player.IsVip ? 0.5 : 1.0;

        if ((DateTimeOffset.UtcNow - player.LastHourly).TotalHours < hoursNeeded)
        {
            var nextAvailable = player.LastHourly.AddHours(hoursNeeded);
            return Result<(int, DateTimeOffset)>.Failure("Время еще не пришло", (player.Balance, nextAvailable));
        }

        // Увеличенная награда для VIP
        int reward = player.IsVip ? 2000 : 1000;

        player.Balance += reward;
        player.LastHourly = DateTimeOffset.UtcNow;
        await _playerRepo.UpdateAsync(player);

        return Result<(int, DateTimeOffset)>.Success((player.Balance, default));
    }

    public async Task<Result<GameState>> StartGameAsync(ulong userId, int bet)
    {
        if (bet < 50) return Result<GameState>.Failure("Минимальная ставка - 50 монет.");
        if (_sessionManager.HasAnyActiveGame(userId)) return Result<GameState>.Failure("Завершите текущую игру!");

        var player = await _playerRepo.GetOrCreateAsync(userId);
        if (player.Balance < bet) return Result<GameState>.Failure($"Недостаточно средств. Баланс: {player.Balance}");

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

        // АКТИВАЦИЯ БУСТЕРОВ
        game.IsBoosted = player.HasActiveBooster;
        game.IsMegaBoosted = player.HasActiveMegaBooster;
        if (game.IsBoosted) player.HasActiveBooster = false;
        if (game.IsMegaBoosted) player.HasActiveMegaBooster = false;

        if (!_sessionManager.TryAddGame(game)) return Result<GameState>.Failure("Завершите текущую игру!");

        player.NextServerSeed = Guid.NewGuid().ToString("N");
        player.NextServerSeedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(player.NextServerSeed))).ToLower();
        player.Balance -= bet;

        var hand = game.Hands[0];
        hand.Cards.Add(game.Deck.Draw());
        game.DealerHand.Add(game.Deck.Draw());
        hand.Cards.Add(game.Deck.Draw());
        game.DealerHand.Add(game.Deck.Draw());

        // Моментальный финал (Блекджек со старта)
        if (hand.Score == 21 || game.DealerScore == 21)
        {
            game.IsGameOver = true;
            player.BjGamesPlayed++;

            if (hand.Score == 21 && game.DealerScore == 21)
            {
                hand.Status = GameStatus.Push;
                player.Balance += bet;
                player.BjDraws++;
            }
            else if (hand.Score == 21)
            {
                int payout = (int)(bet * 2.5);
                int bonus = 0;
                if (game.IsMegaBoosted) bonus = payout;
                else if (game.IsBoosted) { bonus = payout; if (bonus > 50000) bonus = 50000; } // <-- Лимит 100к

                payout += bonus;
                int netProfit = payout - bet;

                hand.Status = GameStatus.BlackjackWin;
                player.Balance += payout;
                player.BjWins++;
                player.Blackjacks++;
                player.BjTotalMoneyWon += netProfit;
            }
            else
            {
                hand.Status = GameStatus.DealerWin;
                player.BjLosses++;
                player.BjTotalMoneyLost += bet;
                RegisterLoss(player, bet); // Регистрация проигрыша
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
        int totalLostInRound = 0;

        foreach (var hand in game.Hands)
        {
            if (hand.Status == GameStatus.PlayerBust)
            {
                player.BjGamesPlayed++;
                player.BjLosses++;
                player.BjTotalMoneyLost += hand.Bet;
                totalLostInRound += hand.Bet;
                continue;
            }

            if (game.DealerScore > 21) hand.Status = GameStatus.DealerBust;
            else if (game.DealerScore > hand.Score) hand.Status = GameStatus.DealerWin;
            else if (game.DealerScore < hand.Score) hand.Status = GameStatus.PlayerWin;
            else hand.Status = GameStatus.Push;

            player.BjGamesPlayed++;

            int payout = hand.Status switch
            {
                GameStatus.DealerBust or GameStatus.PlayerWin => hand.Bet * 2,
                GameStatus.Push => hand.Bet,
                _ => 0
            };

            int bonus = 0;
            // Умножаем выигрыш (но не ничью)
            if (hand.Status is GameStatus.DealerBust or GameStatus.PlayerWin)
            {
                if (game.IsMegaBoosted) bonus = payout;
                else if (game.IsBoosted) { bonus = payout; if (bonus > 50000) bonus = 50000; }
            }

            payout += bonus;
            int netProfit = payout - hand.Bet;

            if (hand.Status == GameStatus.Push) player.BjDraws++;
            else if (netProfit > 0)
            {
                player.BjWins++;
                player.BjTotalMoneyWon += netProfit;
            }
            else
            {
                player.BjLosses++;
                player.BjTotalMoneyLost += hand.Bet;
                totalLostInRound += hand.Bet;
            }
            player.Balance += payout; // Ставка была вычтена в начале, просто прибавляем выплату
        }

        if (totalLostInRound > 0) RegisterLoss(player, totalLostInRound); // Регистрация проигрыша

        await _playerRepo.UpdateAsync(player);
        _sessionManager.RemoveGame(game.UserId);
        await _historyRepo.UpdateToCompletedAsync(game.Id);

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
        if (bet < 50) return Result<CrashGameState>.Failure("Минимальная ставка - 50 монет.");
        if (targetMultiplier < 1.01) return Result<CrashGameState>.Failure("Минимальный множитель - 1.01x.");
        if (_sessionManager.HasAnyActiveGame(userId)) return Result<CrashGameState>.Failure("Завершите текущую игру!");

        var player = await _playerRepo.GetOrCreateAsync(userId);
        if (player.Balance < bet) return Result<CrashGameState>.Failure($"Недостаточно средств. Баланс: {player.Balance}");

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

        player.NextServerSeed = Guid.NewGuid().ToString("N");
        player.NextServerSeedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(player.NextServerSeed))).ToLower();
        player.Balance -= bet;

        byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{serverSeed}:{player.ClientSeed}:{history.Id}"));
        string hex = Convert.ToHexString(hashBytes).ToLower()[..13];
        long h = Convert.ToInt64(hex, 16);
        long e = (long)Math.Pow(2, 52);

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

        // БУСТЕРЫ
        game.IsBoosted = player.HasActiveBooster;
        game.IsMegaBoosted = player.HasActiveMegaBooster;
        if (game.IsBoosted) player.HasActiveBooster = false;
        if (game.IsMegaBoosted) player.HasActiveMegaBooster = false;

        if (game.IsWin)
        {
            int payout = game.Payout;
            int bonus = 0;
            if (game.IsMegaBoosted) bonus = payout;
            else if (game.IsBoosted) { bonus = payout; if (bonus > 50000) bonus = 50000; }

            payout += bonus;
            int netProfit = payout - game.Bet;

            player.Balance += payout;
            player.CrashWins++;
            player.CrashTotalMoneyWon += netProfit;
        }
        else
        {
            player.CrashLosses++;
            player.CrashTotalMoneyLost += game.Bet;
            RegisterLoss(player, game.Bet); // Регистрация проигрыша
        }
        player.CrashGamesPlayed++;

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
        if (bet < 50) return Result<DiceGameState>.Failure("Минимальная ставка - 50 монет.");
        if (min < 1 || max > 100 || min > max) return Result<DiceGameState>.Failure("Диапазон должен быть от 1 до 100.");
        int chance = max - min + 1;
        if (chance > 95) return Result<DiceGameState>.Failure("Максимальный шанс выигрыша — 95%. Выберите меньший диапазон.");
        if (_sessionManager.HasAnyActiveGame(userId)) return Result<DiceGameState>.Failure("Завершите текущую игру!");

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
            GameType = "Dice"
        });

        player.NextServerSeed = Guid.NewGuid().ToString("N");
        player.NextServerSeedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(player.NextServerSeed))).ToLower();
        player.Balance -= bet;

        byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{serverSeed}:{player.ClientSeed}:{history.Id}"));
        string hex = Convert.ToHexString(hashBytes).ToLower()[..8];
        long h = Convert.ToInt64(hex, 16);
        int rolledNumber = (int)(h % 100) + 1;

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

        // БУСТЕРЫ
        game.IsBoosted = player.HasActiveBooster;
        game.IsMegaBoosted = player.HasActiveMegaBooster;
        if (game.IsBoosted) player.HasActiveBooster = false;
        if (game.IsMegaBoosted) player.HasActiveMegaBooster = false;

        if (game.IsWin)
        {
            int payout = game.Payout;
            int bonus = 0;
            if (game.IsMegaBoosted) bonus = payout;
            else if (game.IsBoosted) { bonus = payout; if (bonus > 50000) bonus = 50000; }

            payout += bonus;
            int netProfit = payout - game.Bet;

            player.Balance += payout;
            player.DiceWins++;
            player.DiceTotalMoneyWon += netProfit;
        }
        else
        {
            player.DiceLosses++;
            player.DiceTotalMoneyLost += game.Bet;
            RegisterLoss(player, game.Bet); // Регистрация проигрыша
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

        var tilesWithHashes = Enumerable.Range(0, 20).Select(index =>
        {
            var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{serverSeed}:{player.ClientSeed}:{history.Id}:{index}"));
            return new { Index = index, Hash = Convert.ToHexString(hashBytes).ToLower() };
        }).ToList();
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

        // БУСТЕРЫ
        game.IsBoosted = player.HasActiveBooster;
        game.IsMegaBoosted = player.HasActiveMegaBooster;
        if (game.IsBoosted) player.HasActiveBooster = false;
        if (game.IsMegaBoosted) player.HasActiveMegaBooster = false;

        await _playerRepo.UpdateAsync(player);
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
            game.IsGameOver = true;
            game.IsBusted = true;
            game.BustedOnTile = tileIndex;

            player.MinesGamesPlayed++;
            player.MinesLosses++;
            player.MinesTotalMoneyLost += game.Bet;
            RegisterLoss(player, game.Bet); // Регистрация проигрыша

            await _playerRepo.UpdateAsync(player);
            _sessionManager.RemoveMinesGame(userId);
            await _historyRepo.UpdateToCompletedAsync(game.Id);

            return Result<MinesweeperGameState>.Success(game);
        }

        game.RevealedPositions.Add(tileIndex);

        if (game.RevealedPositions.Count == 20 - game.MinesCount)
            return await CashoutMinesweeperAsync(userId);

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

        int payout = game.CurrentPayout;
        int bonus = 0;
        if (game.IsMegaBoosted) bonus = payout;
        else if (game.IsBoosted) { bonus = payout; if (bonus > 50000) bonus = 50000; }

        payout += bonus;
        int netProfit = payout - game.Bet;

        player.Balance += payout;
        player.MinesGamesPlayed++;
        player.MinesWins++;
        player.MinesTotalMoneyWon += netProfit;

        return Result<MinesweeperGameState>.Success(game);
    }

    public async Task<Result<HiloGameState>> StartHiloAsync(ulong userId, int bet)
    {
        if (bet < 50) return Result<HiloGameState>.Failure("Минимальная ставка - 50 монет.");
        if (_sessionManager.HasAnyActiveGame(userId)) return Result<HiloGameState>.Failure("Завершите текущую игру!");

        var player = await _playerRepo.GetOrCreateAsync(userId);
        if (player.Balance < bet) return Result<HiloGameState>.Failure($"Недостаточно средств. Баланс: {player.Balance}");

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
            GameType = "HiLo"
        });

        player.NextServerSeed = Guid.NewGuid().ToString("N");
        player.NextServerSeedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(player.NextServerSeed))).ToLower();
        player.Balance -= bet;

        var game = new HiloGameState
        {
            Id = history.Id,
            UserId = userId,
            Bet = bet,
            ServerSeed = serverSeed,
            ServerSeedHash = serverSeedHash,
            ClientSeed = player.ClientSeed
        };

        // БУСТЕРЫ
        game.IsBoosted = player.HasActiveBooster;
        game.IsMegaBoosted = player.HasActiveMegaBooster;
        if (game.IsBoosted) player.HasActiveBooster = false;
        if (game.IsMegaBoosted) player.HasActiveMegaBooster = false;

        await _playerRepo.UpdateAsync(player);
        game.DrawnCards.Add(game.DrawCard(0));
        _sessionManager.TryAddHiloGame(game);
        return Result<HiloGameState>.Success(game);
    }

    public async Task<Result<HiloGameState>> GuessHiloAsync(ulong userId, string guess)
    {
        if (!_sessionManager.TryGetHiloGame(userId, out var game) || game is null)
            return Result<HiloGameState>.Failure("Игра не найдена.");

        int round = game.DrawnCards.Count;
        Card prevCard = game.DrawnCards.Last();
        Card nextCard = game.DrawCard(round);

        game.DrawnCards.Add(nextCard);

        bool isWin = false;
        double factor = 1.0;
        int prevRank = (int)prevCard.Rank;

        if (guess == "hi")
        {
            isWin = (int)nextCard.Rank >= prevRank;
            factor = 13.0 / (15 - prevRank);
        }
        else if (guess == "lo")
        {
            isWin = (int)nextCard.Rank <= prevRank;
            factor = 13.0 / (prevRank - 1);
        }

        if (isWin)
        {
            game.CurrentMultiplier = Math.Round(game.CurrentMultiplier * factor, 2);
            return Result<HiloGameState>.Success(game);
        }
        else
        {
            game.IsGameOver = true;
            game.IsBusted = true;

            var player = await _playerRepo.GetOrCreateAsync(userId);
            player.HiloGamesPlayed++;
            player.HiloLosses++;
            player.HiloTotalMoneyLost += game.Bet;
            RegisterLoss(player, game.Bet); // Регистрация проигрыша

            await _playerRepo.UpdateAsync(player);
            _sessionManager.RemoveHiloGame(userId);
            await _historyRepo.UpdateToCompletedAsync(game.Id);

            return Result<HiloGameState>.Success(game);
        }
    }

    public async Task<Result<HiloGameState>> CashoutHiloAsync(ulong userId)
    {
        if (!_sessionManager.TryGetHiloGame(userId, out var game) || game is null)
            return Result<HiloGameState>.Failure("Игра не найдена.");

        if (game.DrawnCards.Count <= 1)
            return Result<HiloGameState>.Failure("Сделайте хотя бы один выбор!");

        game.IsGameOver = true;
        game.IsCashedOut = true;

        var player = await _playerRepo.GetOrCreateAsync(userId);

        int payout = game.CurrentPayout;
        int bonus = 0;
        if (game.IsMegaBoosted) bonus = payout;
        else if (game.IsBoosted) { bonus = payout; if (bonus > 50000) bonus = 50000; }

        payout += bonus;
        int netProfit = payout - game.Bet;

        player.Balance += payout;
        player.HiloGamesPlayed++;
        player.HiloWins++;
        player.HiloTotalMoneyWon += netProfit;

        await _playerRepo.UpdateAsync(player);
        _sessionManager.RemoveHiloGame(userId);
        await _historyRepo.UpdateToCompletedAsync(game.Id);

        return Result<HiloGameState>.Success(game);
    }

    public async Task<Result<(int Balance, DateTimeOffset NextAvailable)>> ClaimDailyAsync(ulong userId)
    {
        var player = await _playerRepo.GetOrCreateAsync(userId);

        if ((DateTimeOffset.UtcNow - player.LastDaily).TotalHours < 24)
        {
            var nextAvailable = player.LastDaily.AddHours(24);
            return Result<(int, DateTimeOffset)>.Failure("Время еще не пришло", (player.Balance, nextAvailable));
        }

        // Увеличенная награда для VIP
        int reward = player.IsVip ? 5000 : 2500;

        player.Balance += reward;
        player.LastDaily = DateTimeOffset.UtcNow;
        await _playerRepo.UpdateAsync(player);

        return Result<(int, DateTimeOffset)>.Success((player.Balance, default));
    }

    public async Task<List<Player>> GetTopPlayersAsync(int count = 10)
    {
        return await _playerRepo.GetTopPlayersAsync(count);
    }

    // Вспомогательный метод для регистрации проигрыша
    private void RegisterLoss(Player player, int lostAmount)
    {
        if (lostAmount > 0)
        {
            player.LastLostBet = lostAmount;
            player.LastLossTime = DateTimeOffset.UtcNow;
            player.IsLastLossRewinded = false;
        }
    }

    public async Task<Result<int>> PreVipCheckAsync(ulong userId)
    {
        var player = await _playerRepo.GetOrCreateAsync(userId);
        if (player.Diamonds < 150) return Result<int>.Failure("Для покупки VIP нужно 150 💎.");
        return Result<int>.Success(150);
    }
    public async Task<Result> ConfirmVipAsync(ulong userId)
    {
        var player = await _playerRepo.GetOrCreateAsync(userId);
        if (player.Diamonds < 150) return Result.Failure("Недостаточно алмазов.");
        player.Diamonds -= 150;
        player.VipUntil = player.IsVip ? player.VipUntil.AddDays(30) : DateTimeOffset.UtcNow.AddDays(30);
        await _playerRepo.UpdateAsync(player);
        return Result.Success();
    }

    public async Task<Result<int>> PreBoosterCheckAsync(ulong userId, bool isMega)
    {
        var player = await _playerRepo.GetOrCreateAsync(userId);
        if (player.HasActiveBooster || player.HasActiveMegaBooster)
            return Result<int>.Failure("У вас уже активирован бустер на следующую игру!");

        int cost = isMega ? 25 : 5;
        if (player.Diamonds < cost)
            return Result<int>.Failure($"Для покупки этого Бустера нужно {cost} 💎.");

        return Result<int>.Success(cost);
    }

    public async Task<Result> ConfirmBoosterAsync(ulong userId, bool isMega)
    {
        var player = await _playerRepo.GetOrCreateAsync(userId);
        if (player.HasActiveBooster || player.HasActiveMegaBooster)
            return Result.Failure("Уже активировано.");

        int cost = isMega ? 25 : 5;
        if (player.Diamonds < cost) return Result.Failure("Недостаточно алмазов.");

        player.Diamonds -= cost;
        if (isMega) player.HasActiveMegaBooster = true;
        else player.HasActiveBooster = true;

        await _playerRepo.UpdateAsync(player);
        return Result.Success();
    }

    public async Task<Result<int>> PrePeekCheckAsync(ulong userId)
    {
        var player = await _playerRepo.GetOrCreateAsync(userId);

        if (_sessionManager.TryGetGame(userId, out var bjGame) && bjGame != null)
        {
            if (bjGame.HasPeeked) return Result<int>.Failure("Вы уже подсматривали карты в этой раздаче!");
        }
        else if (_sessionManager.TryGetHiloGame(userId, out var hiloGame) && hiloGame != null)
        {
            if (hiloGame.HasPeeked) return Result<int>.Failure("Вы уже подсматривали карты в этой раздаче!");
        }
        else return Result<int>.Failure("Просмотр карт доступен только во время активной игры в Блекджек или Выше-Ниже!");

        if (player.Diamonds < 3) return Result<int>.Failure("Для просмотра карт нужно 3 💎.");
        return Result<int>.Success(3);
    }

    public async Task<Result<string>> ConfirmPeekAsync(ulong userId)
    {
        var player = await _playerRepo.GetOrCreateAsync(userId);
        if (player.Diamonds < 3) return Result<string>.Failure("Недостаточно алмазов.");

        string response;
        if (_sessionManager.TryGetGame(userId, out var bjGame) && bjGame != null)
        {
            if (bjGame.HasPeeked) return Result<string>.Failure("Уже использовано.");
            var cards = bjGame.Deck.PeekNext(2);
            response = $"**{cards[0]}** и **{cards[1]}**";
            bjGame.HasPeeked = true; // <-- Ставим флаг
        }
        else if (_sessionManager.TryGetHiloGame(userId, out var hiloGame) && hiloGame != null)
        {
            if (hiloGame.HasPeeked) return Result<string>.Failure("Уже использовано.");
            int round = hiloGame.DrawnCards.Count;
            response = $"**{hiloGame.DrawCard(round)}** и **{hiloGame.DrawCard(round + 1)}**";
            hiloGame.HasPeeked = true; // <-- Ставим флаг
        }
        else return Result<string>.Failure("Игра не найдена.");

        player.Diamonds -= 3;
        await _playerRepo.UpdateAsync(player);
        return Result<string>.Success(response);
    }

    public async Task<Result<(int Cost, int RefundAmount)>> PreRefundCheckAsync(ulong userId)
    {
        var player = await _playerRepo.GetOrCreateAsync(userId);
        if (player.LastLostBet == 0 || player.IsLastLossRewinded)
            return Result<(int, int)>.Failure("У вас нет недавних проигрышей для возврата.");

        if ((DateTimeOffset.UtcNow - player.LastLossTime).TotalMinutes > 5)
            return Result<(int, int)>.Failure("Время вышло! Вернуть ставку можно только в течение 5 минут после проигрыша.");

        int cost = 2 + (int)(player.LastLostBet / 10000);
        if (player.Diamonds < cost)
            return Result<(int, int)>.Failure($"Для возврата ставки ({player.LastLostBet} монет) нужно **{cost} 💎**.");

        int refundAmount = (int)(player.LastLostBet / 2); // Считаем ровно 50%
        return Result<(int, int)>.Success((cost, refundAmount));
    }

    public async Task<Result> ConfirmRefundAsync(ulong userId)
    {
        var player = await _playerRepo.GetOrCreateAsync(userId);
        if (player.LastLostBet == 0 || player.IsLastLossRewinded) return Result.Failure("Нет доступных проигрышей для возврата.");
        int cost = 2 + (int)(player.LastLostBet / 10000);
        if (player.Diamonds < cost) return Result.Failure("Недостаточно алмазов.");

        player.Diamonds -= cost;
        player.Balance += (int)(player.LastLostBet / 2); // Возвращаем 50% на баланс
        player.IsLastLossRewinded = true;

        await _playerRepo.UpdateAsync(player);
        return Result.Success();
    }
}
