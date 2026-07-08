using BlackjackBot.Domain.Entities;
using NetCord;
using NetCord.Rest;

namespace BlackjackBot.Discord.Services;

public static class DiscordMapper
{
    public static EmbedProperties BuildEmbed(GameState game)
    {
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

            color = new Color(0x87CEFA);
            description = $"👤 **Игрок:** <@{game.UserId}>\n💰 **Ставка:** {game.Bet}";
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
                ? new Color(0x98FB98) : (game.Status == GameStatus.Push ? new Color(0xFFE4B5) : new Color(0xFFB6C1));

            description = $"👤 **Игрок:** <@{game.UserId}>\n💰 **Ставка:** {game.Bet}\n\n**Результат:** {result}";
        }

        return new EmbedProperties { Title = "🃏 Блекджек", Description = description, Color = color, Fields = fields };
    }

    public static List<ActionRowProperties> BuildComponents(GameState game)
    {
        if (game.Status == GameStatus.Active)
        {
            return [
                new ActionRowProperties([
                    new ButtonProperties($"bj_hit:{game.UserId}", "Взять", ButtonStyle.Primary),
                    new ButtonProperties($"bj_stand:{game.UserId}", "Хватит", ButtonStyle.Secondary)
                ])
            ];
        }
        return [];
    }
}
