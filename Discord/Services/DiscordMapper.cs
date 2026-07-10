using BlackjackBot.Domain.Entities;
using NetCord;
using NetCord.Rest;

namespace BlackjackBot.Discord.Services;

public static class DiscordMapper
{
    public static EmbedProperties BuildEmbed(GameState game)
    {
        var fields = new List<EmbedFieldProperties>();

        // Отрисовка всех рук игрока
        for (int i = 0; i < game.Hands.Count; i++)
        {
            var hand = game.Hands[i];
            string handName = game.Hands.Count > 1 ? $"Ваша рука {i + 1}" : "Ваша рука";

            if (game.Hands.Count > 1 && i == game.CurrentHandIndex && !game.IsGameOver)
                handName = "👉 " + handName;

            string status = "";
            if (game.IsGameOver || hand.Status == GameStatus.PlayerBust)
            {
                status = hand.Status switch
                {
                    GameStatus.PlayerBust => " [Перебор]",
                    GameStatus.DealerBust => " [Победа]",
                    GameStatus.PlayerWin => " [Победа]",
                    GameStatus.DealerWin => " [Поражение]",
                    GameStatus.Push => " [Ничья]",
                    GameStatus.BlackjackWin => " [Блекджек]",
                    _ => ""
                };
            }

            fields.Add(new() { Name = $"{handName} ({hand.Score}){status}", Value = string.Join(" ", hand.Cards), Inline = true });
        }

        // Отрисовка руки дилера
        if (!game.IsGameOver)
        {
            int visibleScore = GameState.CalculateScore([game.DealerHand[0]]);
            fields.Add(new() { Name = $"Рука дилера ({visibleScore})", Value = $"{game.DealerHand[0]} ❓", Inline = true });
        }
        else
        {
            fields.Add(new() { Name = $"Рука дилера ({game.DealerScore})", Value = string.Join(" ", game.DealerHand), Inline = true });
        }

        Color color = new Color(0x87CEFA);
        int totalBet = game.Hands.Sum(h => h.Bet);

        // Убрали ID отсюда
        string description = $"💰 **Общая ставка:** {totalBet}\n🔒 **Хеш сервера:** `{game.ServerSeedHash}`";

        // Отрисовка общих результатов в конце
        if (game.IsGameOver)
        {
            int totalPayout = game.Hands.Sum(h => h.Status switch {
                GameStatus.DealerBust or GameStatus.PlayerWin => h.Bet * 2,
                GameStatus.BlackjackWin => (int)(h.Bet * 2.5),
                GameStatus.Push => h.Bet,
                _ => 0
            });
            int netProfit = totalPayout - totalBet;

            color = netProfit > 0 ? new Color(0x98FB98) : (netProfit == 0 ? new Color(0xFFE4B5) : new Color(0xFFB6C1));

            string result = netProfit > 0 ? $"Вы выиграли **{totalPayout}** монет!" :
                            (netProfit == 0 ? "Ничья! Ставки возвращены." : "Вы проиграли свои ставки.");

            description += $"\n\n**Результат:** {result}";
        }

        // ВАЖНО: Добавили ID прямо в Title
        return new EmbedProperties { Title = $"🃏 Блекджек (ID: {game.Id})", Description = description, Color = color, Fields = fields };
    }

    // 1. Главная страница профиля
    public static EmbedProperties BuildProfileGeneralEmbed(User user, Player player)
    {
        return new EmbedProperties
        {
            Title = $"📊 Профиль {user.Username}",
            Thumbnail = new EmbedThumbnailProperties(user.HasAvatar ? user.GetAvatarUrl().ToString() : null),
            Color = new Color(0x9B59B6),
            Description = $"**💰 Баланс:** {player.Balance} монет\n\n*👇 Нажмите на кнопки ниже, чтобы посмотреть подробную статистику по каждой игре.*"
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
                new ButtonProperties($"profile_crash:{userId}", "Краш", ButtonStyle.Danger)
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

if h % 20 == 0:
    multiplier = 1.00
else:
    multiplier = math.floor((100.0 * e - h) / (e - h)) / 100.0

print(f'Итоговый множитель: {{multiplier}}x')";
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
            *(На открывшемся сайте нажмите кнопку **Last >>** под кодом, чтобы увидеть результат).*

            📋 **Способ 2 (Ручной):**
            Вставьте код ниже на [Online-Python.com](https://www.online-python.com/) и нажмите **Run**.
            ```python
            {pythonCode}
            ```
            """
        };
    }

    public static EmbedProperties BuildCrashEmbed(CrashGameState game)
    {
        Color color = game.IsWin ? new Color(0x98FB98) : new Color(0xFFB6C1);
        string resultStr = game.IsWin
            ? $"✅ Вы успешно вывели на **{game.TargetMultiplier}x** и выиграли **{game.Payout}** монет!"
            : $"💥 Ракета взорвалась на **{game.ActualMultiplier}x**. Вы не успели вывести ставку.";

        return new EmbedProperties
        {
            Title = $"🚀 Краш (ID: {game.Id})",
            Color = color,
            Description = $"""
            💰 **Ставка:** {game.Bet}
            🎯 **Цель (Автовывод):** {game.TargetMultiplier}x
            🔒 **Хеш сервера:** `{game.ServerSeedHash}`

            📈 **Итоговый множитель: {game.ActualMultiplier}x**

            **Результат:** {resultStr}
            """
        };
    }

    public static EmbedProperties BuildHelpEmbed()
    {
        return new EmbedProperties
        {
            Title = "🤖 Справка по боту",
            Color = new Color(0xF1C40F),
            Description = "Здесь собраны все доступные команды. Вы можете использовать как текстовые команды (начинаются с `!`), так и слэш-команды (`/`).",
            Fields = [
                new() { Name = "🎮 Игры", Value =
                    "`!bj <ставка>` — Сыграть в Блекджек.\n" +
                    "`!crash <ставка> <множитель>` — Сыграть в Краш (например: `!crash 1000 2.5`).",
                    Inline = false },
                new() { Name = "👤 Профиль и Экономика", Value =
                    "`!profile` — Посмотреть свой баланс и статистику.\n" +
                    "`!hourly` — Получить бесплатные монеты (раз в час).",
                    Inline = false },
                new() { Name = "🛡️ Честная игра (Provably Fair)", Value =
                    "`!proof <ID>` — Проверить честность сыгранной игры по её ID.\n" +
                    "`!seed <фраза>` — Задать свою фразу для генерации колоды/результата.\n" +
                    "`!recover` — Выслать кнопки заново, если вы случайно удалили сообщение с активной игрой.",
                    Inline = false }
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
}
