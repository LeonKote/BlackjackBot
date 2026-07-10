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
        // Генерируем готовый Python-скрипт с уже подставленными переменными этой игры!
        // В C# при использовании $@ перед строкой, чтобы вывести { и }, их нужно удваивать {{ }}
        string pythonCode = $@"import hashlib

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

        // Кодируем скрипт, чтобы вставить его в URL-ссылку
        string encodedCode = Uri.EscapeDataString(pythonCode);

        // Ссылка на онлайн-визуализатор Python (код передается прямо в ссылке)
        string directLink = $"https://pythontutor.com/render.html#code={encodedCode}&cumulative=false&heapPrimitives=nevernest&mode=edit&origin=opt-frontend.js&py=3&rawInputLstJSON=%5B%5D&textReferences=false";

        return new EmbedProperties
        {
            Title = $"⚖️ Проверка честности (ID: {history.Id})",
            Color = new Color(0x3498DB),
            Description = $"""
            **Server Seed:** ||`{history.ServerSeed}`||
            **Client Seed:** `{history.ClientSeed}`

            **Как проверить самому?**
            Этот скрипт **уже содержит данные вашей игры**. Мы сделали 2 удобных способа для проверки:

            🔗 **Способ 1 (В один клик):** 
            **[Нажмите сюда, чтобы открыть скрипт]({directLink})**
            *(На открывшемся сайте нажмите кнопку **Last >>** под кодом, и справа в окне Print output появится порядок ваших карт).*

            📋 **Способ 2 (Ручной):**
            Наведите на код ниже, нажмите кнопку копирования справа вверху, вставьте его на [Online-Python.com](https://www.online-python.com/) и нажмите **Run**.

            ```python
            {pythonCode}
            ```
            """
        };
    }
}
