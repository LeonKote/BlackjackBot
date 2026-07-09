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

    public static EmbedProperties BuildProfileEmbed(User user, Player player)
    {
        int winrate = player.GamesPlayed > 0 ? (int)Math.Round((double)player.Wins / player.GamesPlayed * 100) : 0;

        return new EmbedProperties
        {
            Title = $"📊 Профиль {user.Username}",
            Thumbnail = new EmbedThumbnailProperties(user.HasAvatar ? user.GetAvatarUrl().ToString() : null),
            Color = new Color(0x9B59B6),
            Fields = [
                new() { Name = "💰 Баланс", Value = $"{player.Balance} монет", Inline = false },
                new() { Name = "🎮 Сыграно", Value = player.GamesPlayed.ToString(), Inline = true },
                new() { Name = "🏆 Побед", Value = player.Wins.ToString(), Inline = true },
                new() { Name = "📈 Винрейт", Value = $"{winrate}%", Inline = true },
                new() { Name = "💀 Поражений", Value = player.Losses.ToString(), Inline = true },
                new() { Name = "🤝 Ничьих", Value = player.Draws.ToString(), Inline = true },
                new() { Name = "🃏 Блекджеков", Value = player.Blackjacks.ToString(), Inline = true }
            ]
        };
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
        return new EmbedProperties
        {
            Title = $"⚖️ Проверка честности (ID: {history.Id})",
            Color = new Color(0x3498DB),
            Description = $"""
            **Server Seed:** ||`{history.ServerSeed}`||
            **Server Seed Hash:** `{history.ServerSeedHash}`
            **Client Seed:** `{history.ClientSeed}`
            **Game ID:** `{history.Id}`
            
            **Как алгоритм тасует колоду?**
            1. Изначально берется 52 карты (от Двоек до Тузов по порядку: ♥️, ♦️, ♣️, ♠️).
            2. Для каждой карты (индекс от 0 до 51) вычисляется **SHA256-хеш** от строки:
            `ServerSeed:ClientSeed:GameID:Index`
            *(Пример для первой карты: `{history.ServerSeed}:{history.ClientSeed}:{history.Id}:0`)*
            3. 52 карты сортируются по полученным хешам в алфавитном порядке (от меньшего к большему).
            4. Карта с самым маленьким хешем сдается первой.

            *Этот алгоритм гарантирует абсолютную случайность и исключает подкрутку. Вы можете легко повторить его сами на Python, C# или JS, чтобы сверить выданные вам карты!*
            """
        };
    }
}
