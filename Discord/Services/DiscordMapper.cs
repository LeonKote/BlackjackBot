using BlackjackBot.Domain.Entities;
using NetCord;
using NetCord.Rest;

namespace BlackjackBot.Discord.Services;

public static class DiscordMapper
{
    public static EmbedProperties BuildEmbed(GameState game)
    {
        var fields = new List<EmbedFieldProperties>();

        for (int i = 0; i < game.Hands.Count; i++)
        {
            var hand = game.Hands[i];
            string handName = game.Hands.Count > 1 ? $"Ваша рука {i + 1}" : "Ваша рука";
            if (game.Hands.Count > 1 && i == game.CurrentHandIndex && !game.IsGameOver) handName = "👉 " + handName;

            string status = "";
            if (game.IsGameOver || hand.Status == GameStatus.PlayerBust)
            {
                status = hand.Status switch { GameStatus.PlayerBust => " [Перебор]", GameStatus.DealerBust => " [Победа]", GameStatus.PlayerWin => " [Победа]", GameStatus.DealerWin => " [Поражение]", GameStatus.Push => " [Ничья]", GameStatus.BlackjackWin => " [Блекджек]", _ => "" };
            }
            fields.Add(new() { Name = $"{handName} ({hand.Score}){status}", Value = string.Join(" ", hand.Cards), Inline = true });
        }

        if (!game.IsGameOver) fields.Add(new() { Name = $"Рука дилера ({GameState.CalculateScore([game.DealerHand[0]])})", Value = $"{game.DealerHand[0]} ❓", Inline = true });
        else fields.Add(new() { Name = $"Рука дилера ({game.DealerScore})", Value = string.Join(" ", game.DealerHand), Inline = true });

        Color color = new Color(0x2980B9);
        int totalBet = game.Hands.Sum(h => h.Bet);
        string description = $"💰 **Общая ставка:** {totalBet}\n🔒 **Хеш сервера:** `{game.ServerSeedHash}`";

        if (game.IsMegaBoosted) description += "\n🔥 **МЕГА-БУСТЕР АКТИВЕН (Выигрыш x2 без лимитов)**";
        else if (game.IsBoosted) description += "\n🚀 **БУСТЕР АКТИВЕН (Выигрыш x2 до 50k монет)**";

        if (game.IsGameOver)
        {
            int totalPayout = game.Hands.Sum(h => h.Status switch { GameStatus.DealerBust or GameStatus.PlayerWin => h.Bet * 2, GameStatus.BlackjackWin => (int)(h.Bet * 2.5), GameStatus.Push => h.Bet, _ => 0 });
            int netProfit = totalPayout - totalBet;

            color = netProfit > 0 ? new Color(0x2ECC71) : (netProfit == 0 ? new Color(0xF39C12) : new Color(0xE74C3C));
            string result = netProfit > 0 ? $"Вы выиграли **{totalPayout}** монет!" : (netProfit == 0 ? "Ничья! Ставки возвращены." : "Вы проиграли свои ставки.");
            description += $"\n\n**Результат:** {result}";
        }
        return new EmbedProperties { Title = $"🃏 Блекджек (ID: {game.Id})", Description = description, Color = color, Fields = fields };
    }

    // 1. Главная страница профиля
    public static EmbedProperties BuildProfileGeneralEmbed(User user, Player player)
    {
        string vipStatus = player.IsVip ? $"\n👑 **VIP Статус:** активен до <t:{player.VipUntil.ToUnixTimeSeconds()}:d>" : "";

        return new EmbedProperties
        {
            Title = $"📊 Профиль {user.Username}",
            Thumbnail = new EmbedThumbnailProperties(user.HasAvatar ? user.GetAvatarUrl().ToString() : null),
            Color = new Color(0x9B59B6),
            Description = $"**💰 Баланс:** {player.Balance:N0} монет\n" +
                          $"**💎 Алмазы:** {player.Diamonds:N0}{vipStatus}\n\n" +
                          $"*👇 Нажмите на кнопки ниже, чтобы посмотреть подробную статистику по каждой игре.*"
        };
    }

    // 2. Статистика Блекджека
    public static EmbedProperties BuildProfileBjEmbed(User user, Player player)
    {
        int bjWinrate = player.BjGamesPlayed > 0 ? (int)Math.Round((double)player.BjWins / player.BjGamesPlayed * 100) : 0;

        return new EmbedProperties
        {
            Title = $"🃏 Статистика Блекджека ({user.Username})",
            Thumbnail = new EmbedThumbnailProperties(user.HasAvatar ? user.GetAvatarUrl().ToString() : null),
            Color = new Color(0x3498DB),
            Fields = [
                new() { Name = "🎮 Сыграно", Value = player.BjGamesPlayed.ToString(), Inline = true },
                new() { Name = "🏆 Побед / 💀 Поражений", Value = $"{player.BjWins} / {player.BjLosses}", Inline = true },
                new() { Name = "📈 Винрейт", Value = $"{bjWinrate}%", Inline = true },
                new() { Name = "🤝 Ничьих / 🃏 Блекджеков", Value = $"{player.BjDraws} / {player.Blackjacks}", Inline = true },
                new() { Name = "💵 Выиграно", Value = $"+{player.BjTotalMoneyWon:N0}", Inline = true },
                new() { Name = "💸 Проиграно", Value = $"-{player.BjTotalMoneyLost:N0}", Inline = true }
            ]
        };
    }

    // 3. Статистика Краша
    public static EmbedProperties BuildProfileCrashEmbed(User user, Player player)
    {
        int crashWinrate = player.CrashGamesPlayed > 0 ? (int)Math.Round((double)player.CrashWins / player.CrashGamesPlayed * 100) : 0;

        return new EmbedProperties
        {
            Title = $"🚀 Статистика Краша ({user.Username})",
            Thumbnail = new EmbedThumbnailProperties(user.HasAvatar ? user.GetAvatarUrl().ToString() : null),
            Color = new Color(0xE74C3C),
            Fields = [
                new() { Name = "🎮 Сыграно", Value = player.CrashGamesPlayed.ToString(), Inline = true },
                new() { Name = "🏆 Побед / 💀 Поражений", Value = $"{player.CrashWins} / {player.CrashLosses}", Inline = true },
                new() { Name = "📈 Винрейт", Value = $"{crashWinrate}%", Inline = true },
                new() { Name = "💵 Выиграно", Value = $"+{player.CrashTotalMoneyWon:N0}", Inline = true },
                new() { Name = "💸 Проиграно", Value = $"-{player.CrashTotalMoneyLost:N0}", Inline = true }
            ]
        };
    }

    // 4. Кнопки навигации профиля
    public static List<ActionRowProperties> BuildProfileComponents(ulong userId)
    {
        return [
            new ActionRowProperties([
                new ButtonProperties($"profile_general:{userId}", "Главная", ButtonStyle.Secondary),
                new ButtonProperties($"profile_bj:{userId}", "Блекджек", ButtonStyle.Primary),
                new ButtonProperties($"profile_crash:{userId}", "Краш", ButtonStyle.Danger),
                new ButtonProperties($"profile_dice:{userId}", "Дайс", ButtonStyle.Success),
                new ButtonProperties($"profile_mines:{userId}", "Сапёр", ButtonStyle.Secondary) // <-- Новая кнопка
            ]),
            new ActionRowProperties([
                new ButtonProperties($"profile_hilo:{userId}", "Выше-Ниже", ButtonStyle.Primary)
            ])
        ];
    }

    public static List<ActionRowProperties> BuildComponents(GameState game)
    {
        if (game.IsGameOver) return [];

        var hand = game.CurrentHand;

        // Условия для появления кнопок
        bool canDouble = hand.Cards.Count == 2;
        bool canSplit = hand.Cards.Count == 2 && hand.Cards[0].Rank == hand.Cards[1].Rank && game.Hands.Count < 4;

        var buttons = new List<ButtonProperties>
        {
            new ButtonProperties($"bj_hit:{game.UserId}", "Взять", ButtonStyle.Primary),
            new ButtonProperties($"bj_stand:{game.UserId}", "Хватит", ButtonStyle.Secondary)
        };

        if (canDouble)
            buttons.Add(new ButtonProperties($"bj_double:{game.UserId}", "Дабл", ButtonStyle.Success));

        if (canSplit)
            buttons.Add(new ButtonProperties($"bj_split:{game.UserId}", "Сплит", ButtonStyle.Danger));

        return [new ActionRowProperties(buttons)];
    }

    public static EmbedProperties BuildProofEmbed(GameHistory history)
    {
        string pythonCode;
        if (history.GameType == "Crash")
        {
            pythonCode = $@"import hashlib
import math

server = '{history.ServerSeed}'
client = '{history.ClientSeed}'
game_id = {history.Id}

s = f'{{server}}:{{client}}:{{game_id}}'
h_hex = hashlib.sha256(s.encode()).hexdigest()[:13]
h = int(h_hex, 16)
e = 2**52

# Расчет без House Edge
multiplier = math.floor((100.0 * e) / (e - h)) / 100.0

print(f'Итоговый множитель: {{multiplier}}x')";
        }
        else if (history.GameType == "Dice")
        {
            pythonCode = $@"import hashlib

server = '{history.ServerSeed}'
client = '{history.ClientSeed}'
game_id = {history.Id}

s = f'{{server}}:{{client}}:{{game_id}}'
h_hex = hashlib.sha256(s.encode()).hexdigest()[:8]
h = int(h_hex, 16)

rolled_number = (h % 100) + 1
print(f'Выпавшее число: {{rolled_number}}')";
        }
        else if (history.GameType == "Minesweeper")
        {
            pythonCode = $@"import hashlib

server = '{history.ServerSeed}'
client = '{history.ClientSeed}'
game_id = {history.Id}

tiles = []
for i in range(20):
    s = f'{{server}}:{{client}}:{{game_id}}:{{i}}'
    h = hashlib.sha256(s.encode()).hexdigest()
    tiles.append((h, i))

tiles.sort(key=lambda x: x[0])
mines = [t[1] for t in tiles] # Отсортированные индексы

print('Позиции бомб (в зависимости от их количества, берите первые N чисел):')
print(mines)";
        }
        else if (history.GameType == "HiLo")
        {
            pythonCode = $@"import hashlib

server = '{history.ServerSeed}'
client = '{history.ClientSeed}'
game_id = {history.Id}

suits = ['♥️', '♦️', '♣️', '♠️']
ranks = ['2','3','4','5','6','7','8','9','10','J','Q','K','A']

print('Выпавшие карты:')
for round_idx in range(10): # Показываем первые 10 выданных карт
    s = f'{{server}}:{{client}}:{{game_id}}:{{round_idx}}'
    h_hex = hashlib.sha256(s.encode()).hexdigest()[:8]
    h = int(h_hex, 16)
    card_index = h % 52
    
    suit = suits[card_index // 13]
    rank = ranks[(card_index % 13)]
    
    print(f'Раунд {{round_idx}}: {{suit}}{{rank}}')";
        }
        else
        {
            pythonCode = $@"import hashlib

server = '{history.ServerSeed}'
client = '{history.ClientSeed}'
game_id = {history.Id}

suits = ['♥️', '♦️', '♣️', '♠️']
ranks = ['2','3','4','5','6','7','8','9','10','J','Q','K','A']
deck = [f'{{s}}{{r}}' for s in suits for r in ranks]

cards = []
for i in range(52):
    s = f'{{server}}:{{client}}:{{game_id}}:{{i}}'
    h = hashlib.sha256(s.encode()).hexdigest()
    cards.append((h, deck[i]))

cards.sort(key=lambda x: x[0])
print('Раздача карт:', ', '.join(c[1] for c in cards[:10]))";
        }

        string encodedCode = Uri.EscapeDataString(pythonCode);
        string directLink = $"https://pythontutor.com/render.html#code={encodedCode}&cumulative=false&heapPrimitives=nevernest&mode=edit&origin=opt-frontend.js&py=3&rawInputLstJSON=%5B%5D&textReferences=false";

        return new EmbedProperties
        {
            Title = $"⚖️ Проверка честности (ID: {history.Id})",
            Color = new Color(0x3498DB),
            Description = $"""
            **Игра:** {history.GameType}
            **Server Seed:** ||`{history.ServerSeed}`||
            **Client Seed:** `{history.ClientSeed}`

            🔗 **Способ 1 (В один клик):** 
            **[Нажмите сюда, чтобы открыть скрипт]({directLink})**
            *(Нажмите кнопку **Last >>** под кодом).*
            """
        };
    }

    public static EmbedProperties BuildCrashEmbed(CrashGameState game)
    {
        Color color = game.IsWin ? new Color(0x2ECC71) : new Color(0xE74C3C);
        string resultStr = game.IsWin ? $"✅ Вы успешно вывели на **{game.TargetMultiplier}x** и выиграли **{game.Payout}** монет!" : $"💥 Ракета взорвалась на **{game.ActualMultiplier}x**. Вы не успели вывести ставку.";

        string description = $"💰 **Ставка:** {game.Bet}\n🎯 **Цель (Автовывод):** {game.TargetMultiplier}x\n🔒 **Хеш сервера:** `{game.ServerSeedHash}`";
        if (game.IsMegaBoosted) description += "\n🔥 **МЕГА-БУСТЕР АКТИВЕН (Выигрыш x2 без лимитов)**";
        else if (game.IsBoosted) description += "\n🚀 **БУСТЕР АКТИВЕН (Выигрыш x2 до 50k монет)**";

        description += $"\n\n📈 **Итоговый множитель: {game.ActualMultiplier}x**\n\n**Результат:** {resultStr}";
        return new EmbedProperties { Title = $"🚀 Краш (ID: {game.Id})", Color = color, Description = description };
    }

    public static EmbedProperties BuildHelpEmbed()
    {
        return new EmbedProperties
        {
            Title = "🤖 Справка по боту",
            Color = new Color(0xF1C40F),
            Description = "Здесь собраны все доступные команды. Вы можете использовать как текстовые команды (`!`), так и слэш-команды (`/`).",
            Fields = [
                new() { Name = "🎮 Игры", Value = "`!bj <ставка>` — Блекджек.\n`!crash <ставка> <множ.>` — Краш.\n`!dice <ставка> <от> <до>` — Дайс.\n`!mines <ставка> <бомбы 1-19>` — Сапёр.\n`!hilo <ставка>` — Выше-Ниже.", Inline = false },
                new() { Name = "👤 Экономика", Value = "`!profile` — Профиль.\n`!top` — Топ игроков.\n`!hourly` — Бонус раз в час.\n`!daily` — Бонус раз в 24 часа.", Inline = false },
                new() { Name = "💎 Премиум (Магазин: `!shop`)", Value = "`!booster [mega]` — Купить бустер x2 на следующую игру.\n`!peek` — Подсмотреть следующие карты в игре.\n`!refund` — Отменить проигрыш и вернуть ставку.\n`!vip` — Купить VIP статус.", Inline = false },
                new() { Name = "🛡️ Честная игра", Value = "`!proof <ID>` — Проверить честность.\n`!nextseed` — Узнать хеш следующей игры.\n`!seed <фраза>` — Задать свою фразу.\n`!recover` — Восстановить меню игры.", Inline = false }
            ]
        };
    }

    public static EmbedProperties BuildNextSeedEmbed(string serverSeedHash, string clientSeed)
    {
        return new EmbedProperties
        {
            Title = "🔒 Ваши сиды для следующей игры",
            Color = new Color(0x3498DB),
            Description = $"""
            **Хеш следующего Server Seed:** 
            `{serverSeedHash}`

            **Ваш текущий Client Seed:** 
            `{clientSeed}`

            *Перед началом каждой игры сервер заранее генерирует сид и показывает вам его хеш (он написан выше).*
            *После того как вы сыграете, вы сможете сверить, что сервер использовал именно этот сид, а не подменил его!*
            """
        };
    }

    public static EmbedProperties BuildDiceEmbed(DiceGameState game)
    {
        Color color = game.IsWin ? new Color(0x2ECC71) : new Color(0xE74C3C);
        string resultStr = game.IsWin ? $"✅ Выпало число **{game.RolledNumber}**! Вы выиграли **{game.Payout}** монет!" : $"❌ Выпало число **{game.RolledNumber}**. Ставка проиграна.";

        string description = $"💰 **Ставка:** {game.Bet}\n🎯 **Ваш диапазон:** от {game.MinNumber} до {game.MaxNumber} (Шанс: {game.MaxNumber - game.MinNumber + 1}%)\n✖️ **Множитель выигрыша:** {game.Multiplier}x\n🔒 **Хеш сервера:** `{game.ServerSeedHash}`";
        if (game.IsMegaBoosted) description += "\n🔥 **МЕГА-БУСТЕР АКТИВЕН (Выигрыш x2 без лимитов)**";
        else if (game.IsBoosted) description += "\n🚀 **БУСТЕР АКТИВЕН (Выигрыш x2 до 50k монет)**";

        description += $"\n\n🎲 **Выпавшее число: {game.RolledNumber}**\n\n**Результат:** {resultStr}";
        return new EmbedProperties { Title = $"🎲 Дайс (ID: {game.Id})", Color = color, Description = description };
    }

    public static EmbedProperties BuildProfileDiceEmbed(User user, Player player)
    {
        int diceWinrate = player.DiceGamesPlayed > 0 ? (int)Math.Round((double)player.DiceWins / player.DiceGamesPlayed * 100) : 0;

        return new EmbedProperties
        {
            Title = $"🎲 Статистика Дайса ({user.Username})",
            Thumbnail = new EmbedThumbnailProperties(user.HasAvatar ? user.GetAvatarUrl().ToString() : null),
            Color = new Color(0x2ECC71),
            Fields = [
                new() { Name = "🎮 Сыграно", Value = player.DiceGamesPlayed.ToString(), Inline = true },
                new() { Name = "🏆 Побед / 💀 Поражений", Value = $"{player.DiceWins} / {player.DiceLosses}", Inline = true },
                new() { Name = "📈 Винрейт", Value = $"{diceWinrate}%", Inline = true },
                new() { Name = "💵 Выиграно", Value = $"+{player.DiceTotalMoneyWon:N0}", Inline = true },
                new() { Name = "💸 Проиграно", Value = $"-{player.DiceTotalMoneyLost:N0}", Inline = true }
            ]
        };
    }

    public static EmbedProperties BuildMinesweeperEmbed(MinesweeperGameState game)
    {
        Color color = new Color(0x2980B9);
        string resultStr = "Осторожно открывайте плитки или заберите выигрыш.";

        if (game.IsGameOver)
        {
            color = game.IsCashedOut ? new Color(0x2ECC71) : new Color(0xE74C3C);
            resultStr = game.IsCashedOut ? $"✅ Вы успешно вывели **{game.CurrentPayout}** монет на множителе **{game.CurrentMultiplier}x**!" : $"💥 Вы нарвались на мину! Ставка проиграна.";
        }

        string description = $"💰 **Ставка:** {game.Bet}\n💣 **Количество мин:** {game.MinesCount}\n✖️ **Текущий множитель:** {game.CurrentMultiplier}x\n💵 **Возможный выигрыш:** {game.CurrentPayout}\n🔒 **Хеш сервера:** `{game.ServerSeedHash}`";
        if (game.IsMegaBoosted) description += "\n🔥 **МЕГА-БУСТЕР АКТИВЕН (Выигрыш x2 без лимитов)**";
        else if (game.IsBoosted) description += "\n🚀 **БУСТЕР АКТИВЕН (Выигрыш x2 до 50k монет)**";

        description += $"\n\n**Результат:** {resultStr}";
        return new EmbedProperties { Title = $"💣 Сапёр (ID: {game.Id})", Color = color, Description = description };
    }

    public static List<ActionRowProperties> BuildMinesweeperComponents(MinesweeperGameState game)
    {
        var rows = new List<ActionRowProperties>();

        // Отрисовка сетки 5x4 (20 плиток)
        for (int r = 0; r < 4; r++)
        {
            var buttons = new List<ButtonProperties>();
            for (int c = 0; c < 5; c++)
            {
                int index = r * 5 + c;
                string label = "⬜"; // Смайлик теперь прямо в тексте кнопки
                ButtonStyle style = ButtonStyle.Secondary;
                bool disabled = game.IsGameOver;

                if (game.RevealedPositions.Contains(index))
                {
                    label = "💎";
                    style = ButtonStyle.Success;
                    disabled = true;
                }
                else if (game.IsGameOver) // Показываем скрытые плитки в конце игры
                {
                    if (game.MinePositions.Contains(index))
                    {
                        label = index == game.BustedOnTile ? "💥" : "💣";
                        style = ButtonStyle.Danger;
                    }
                    else
                    {
                        label = "💎";
                        style = ButtonStyle.Secondary; // Безопасные, но не открытые
                    }
                }

                // Передаем label напрямую, убрали свойство Emoji
                var btn = new ButtonProperties($"mines_click:{game.UserId}:{index}", label, style)
                {
                    Disabled = disabled
                };
                buttons.Add(btn);
            }
            rows.Add(new ActionRowProperties(buttons));
        }

        // 5-й ряд: Кнопка вывода (только если игра активна и есть открытые плитки)
        if (!game.IsGameOver)
        {
            rows.Add(new ActionRowProperties([
                new ButtonProperties($"mines_cashout:{game.UserId}", $"💰 Забрать {game.CurrentPayout}", ButtonStyle.Primary)
                {
                    Disabled = game.RevealedPositions.Count == 0
                }
            ]));
        }

        return rows;
    }

    public static EmbedProperties BuildProfileMinesEmbed(User user, Player player)
    {
        int minesWinrate = player.MinesGamesPlayed > 0 ? (int)Math.Round((double)player.MinesWins / player.MinesGamesPlayed * 100) : 0;

        return new EmbedProperties
        {
            Title = $"💣 Статистика Сапёра ({user.Username})",
            Thumbnail = new EmbedThumbnailProperties(user.HasAvatar ? user.GetAvatarUrl().ToString() : null),
            Color = new Color(0xF39C12),
            Fields = [
                new() { Name = "🎮 Сыграно", Value = player.MinesGamesPlayed.ToString(), Inline = true },
                new() { Name = "🏆 Побед / 💀 Поражений", Value = $"{player.MinesWins} / {player.MinesLosses}", Inline = true },
                new() { Name = "📈 Винрейт", Value = $"{minesWinrate}%", Inline = true },
                new() { Name = "💵 Выиграно", Value = $"+{player.MinesTotalMoneyWon:N0}", Inline = true },
                new() { Name = "💸 Проиграно", Value = $"-{player.MinesTotalMoneyLost:N0}", Inline = true }
            ]
        };
    }

    public static EmbedProperties BuildHiloEmbed(HiloGameState game)
    {
        Color color = new Color(0x2980B9);
        string resultStr = "Угадайте, какой будет следующая карта.";

        if (game.IsGameOver)
        {
            color = game.IsCashedOut ? new Color(0x2ECC71) : new Color(0xE74C3C);
            resultStr = game.IsCashedOut ? $"✅ Вы успешно вывели **{game.CurrentPayout}** монет на множителе **{game.CurrentMultiplier}x**!" : $"💥 Вы не угадали! Ставка проиграна.";
        }

        string history = string.Join(" ➡️ ", game.DrawnCards.TakeLast(8));
        string description = $"💰 **Ставка:** {game.Bet}\n✖️ **Текущий множитель:** {game.CurrentMultiplier}x\n💵 **Возможный выигрыш:** {game.CurrentPayout}\n🔒 **Хеш сервера:** `{game.ServerSeedHash}`";
        if (game.IsMegaBoosted) description += "\n🔥 **МЕГА-БУСТЕР АКТИВЕН (Выигрыш x2 без лимитов)**";
        else if (game.IsBoosted) description += "\n🚀 **БУСТЕР АКТИВЕН (Выигрыш x2 до 50k монет)**";

        description += $"\n\n🃏 **Карты:** {history}\n\n**Результат:** {resultStr}";
        return new EmbedProperties { Title = $"🃏 Выше-Ниже (ID: {game.Id})", Color = color, Description = description };
    }

    public static List<ActionRowProperties> BuildHiloComponents(HiloGameState game)
    {
        if (game.IsGameOver) return [];

        Card currentCard = game.DrawnCards.Last();
        int rankValue = (int)currentCard.Rank;

        // Высчитываем и показываем игроку вероятности выигрыша на кнопке!
        double probHi = (15.0 - rankValue) / 13.0;
        double probLo = (rankValue - 1.0) / 13.0;

        return [
            new ActionRowProperties([
                new ButtonProperties($"hilo_guess:{game.UserId}:hi", $"⬆️ Выше или равно ({(probHi * 100):0}%)", ButtonStyle.Primary),
                new ButtonProperties($"hilo_guess:{game.UserId}:lo", $"⬇️ Ниже или равно ({(probLo * 100):0}%)", ButtonStyle.Danger)
            ]),
            new ActionRowProperties([
                new ButtonProperties($"hilo_cashout:{game.UserId}", $"💰 Забрать {game.CurrentPayout}", ButtonStyle.Success)
                {
                    Disabled = game.DrawnCards.Count <= 1
                }
            ])
        ];
    }

    public static EmbedProperties BuildProfileHiloEmbed(User user, Player player)
    {
        int hiloWinrate = player.HiloGamesPlayed > 0 ? (int)Math.Round((double)player.HiloWins / player.HiloGamesPlayed * 100) : 0;

        return new EmbedProperties
        {
            Title = $"🃏 Статистика Выше-Ниже ({user.Username})",
            Thumbnail = new EmbedThumbnailProperties(user.HasAvatar ? user.GetAvatarUrl().ToString() : null),
            Color = new Color(0x9B59B6),
            Fields = [
                new() { Name = "🎮 Сыграно", Value = player.HiloGamesPlayed.ToString(), Inline = true },
                new() { Name = "🏆 Побед / 💀 Поражений", Value = $"{player.HiloWins} / {player.HiloLosses}", Inline = true },
                new() { Name = "📈 Винрейт", Value = $"{hiloWinrate}%", Inline = true },
                new() { Name = "💵 Выиграно", Value = $"+{player.HiloTotalMoneyWon:N0}", Inline = true },
                new() { Name = "💸 Проиграно", Value = $"-{player.HiloTotalMoneyLost:N0}", Inline = true }
            ]
        };
    }

    public static EmbedProperties BuildLeaderboardEmbed(List<Player> topPlayers)
    {
        // Генерируем строки. 1, 2 и 3 место получают красивые медали.
        var description = string.Join("\n", topPlayers.Select((p, i) =>
        {
            string medal = i switch { 0 => "🥇", 1 => "🥈", 2 => "🥉", _ => $"**{i + 1}.**" };
            return $"{medal} <@{p.Id}> — 💰 **{p.Balance:N0}** монет";
        }));

        if (string.IsNullOrWhiteSpace(description)) description = "Пока никого нет.";

        return new EmbedProperties
        {
            Title = "🏆 Топ-10 богачей сервера",
            Color = new Color(0xF1C40F),
            Description = description
        };
    }

    public static string GetRandomHourlyMessage(ulong userId)
    {
        string[] quotes = [
            $"<@{userId}> наклянчил косарь нищук.",
            $"Швырнул 1000 монет в лицо <@{userId}>. Иди лудомань.",
            $"<@{userId}> опять просит милостыню. Выдал 1000 мелочью.",
            $"Очередная подачка для <@{userId}>: 1000 монет.",
            $"Сжалился над <@{userId}> и кинул косарь на пропитание.",
            $"Держи косарь, <@{userId}>, и не плачь когда сольешь его в краше."
        ];
        return quotes[Random.Shared.Next(quotes.Length)];
    }

    public static string GetRandomDailyMessage(ulong userId)
    {
        string[] quotes = [
            $"Одобрил заявку <@{userId}> на пособие по лудомании (+2500 монет).",
            $"<@{userId}> получил 2500 на бедность. Гуляй, рванина!",
            $"Целых 2500 для <@{userId}>! Хватит ровно на две секунды в сапёре.",
            $"Большой куш для нищука. <@{userId}> выпросил 2500 монет.",
            $"С барского плеча выделил 2500 для <@{userId}>. До завтра не возвращайся.",
            $"Вытряхнул из кассы 2500 монет для <@{userId}>. Не слей всё за одну раздачу."
        ];
        return quotes[Random.Shared.Next(quotes.Length)];
    }

    public static EmbedProperties BuildShopEmbed()
    {
        return new EmbedProperties
        {
            Title = "🛒 Магазин Алмазов (💎)",
            Color = new Color(0x9B59B6),
            Description = "Алмазы позволяют покупать VIP статус и использовать премиум-функции во время игр!\n\n" +
                          "**Прайс-лист:**\n" +
                          "🥉 **50 💎** — 99 рублей\n" +
                          "🥈 **150 💎** — 279 рублей\n" +
                          "🥇 **500 💎** — 899 рублей\n" +
                          "💎 **1800 💎** — 2890 рублей\n" +
                          "👑 **6500 💎** — 9490 рублей\n\n" +
                          "*(Для покупки алмазов обратитесь к администратору сервера)*"
        };
    }

    public static EmbedProperties BuildConfirmationEmbed(string title, string description)
    {
        return new EmbedProperties
        {
            Title = $"❓ Подтверждение: {title}",
            Color = new Color(0xF1C40F),
            Description = description
        };
    }

    public static List<ActionRowProperties> BuildConfirmationComponents(string actionType, ulong userId)
    {
        return [
            new ActionRowProperties([
                new ButtonProperties($"confirm_{actionType}:{userId}", "✅ Подтвердить", ButtonStyle.Success),
                new ButtonProperties($"cancel_action:{userId}", "❌ Отмена", ButtonStyle.Danger)
            ])
        ];
    }
}
