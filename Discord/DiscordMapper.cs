using BlackjackBot.Domain.Entities;
using NetCord;
using NetCord.Rest;

namespace BlackjackBot.Discord;

public static class DiscordMapper
{
    public static InteractionMessageProperties ToDiscordMessage(this GameState game)
    {
        // NetCord использует EmbedFieldProperties вместо AddField
        var fields = new List<EmbedFieldProperties>
            {
                new() { Name = $"Ваша рука ({game.PlayerScore})", Value = string.Join(" ", game.PlayerHand), Inline = true }
            };

        Color color;
        string description;

        if (game.Status == GameStatus.Active)
        {
            int visibleScore = GameState.CalculateScore([game.DealerHand[0]]);
            fields.Add(new() { Name = $"Рука дилера ({visibleScore})", Value = $"{game.DealerHand[0]} ❓", Inline = true });

            color = new Color(0x3498db); // Синий
            description = $"**Ставка:** {game.Bet}";
        }
        else
        {
            fields.Add(new() { Name = $"Рука дилера ({game.DealerScore})", Value = string.Join(" ", game.DealerHand), Inline = true });

            string result = game.Status switch
            {
                GameStatus.PlayerBust => "Вы перебрали! Ставка проиграна.",
                GameStatus.DealerBust => $"Дилер перебрал! Вы выиграли **{game.Bet * 2}**.",
                GameStatus.PlayerWin => $"Вы победили! Вы выиграли **{game.Bet * 2}**.",
                GameStatus.DealerWin => "Дилер победил! Ставка проиграна.",
                GameStatus.Push => "Ничья! Ставка возвращена.",
                GameStatus.BlackjackWin => $"Блекджек! Вы выиграли **{(int)(game.Bet * 2.5)}**.",
                _ => ""
            };

            color = game.Status is GameStatus.PlayerWin or GameStatus.DealerBust or GameStatus.BlackjackWin
                ? new Color(0x2ecc71) : (game.Status == GameStatus.Push ? new Color(0xf39c12) : new Color(0xe74c3c));

            description = $"**Ставка:** {game.Bet}\n\n**Результат:** {result}";
        }

        // В NetCord мы собираем Embed через инициализатор свойств
        var embed = new EmbedProperties
        {
            Title = "🃏 Блекджек",
            Description = description,
            Color = color,
            Fields = fields
        };

        var msg = new InteractionMessageProperties
        {
            Embeds = [embed]
        };

        // Добавление компонентов также происходит через коллекции, а не AddComponents
        if (game.Status == GameStatus.Active)
        {
            msg.Components = [
                new ActionRowProperties([
                        new ButtonProperties($"bj_hit:{game.UserId}", "Взять", ButtonStyle.Primary),
                        new ButtonProperties($"bj_stand:{game.UserId}", "Хватит", ButtonStyle.Secondary)
                    ])
            ];
        }

        return msg;
    }
}
