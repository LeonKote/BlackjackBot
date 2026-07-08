using BlackjackBot.Application.Interfaces;
using BlackjackBot.Application.Services;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace BlackjackBot.Discord;

public class CommandModule : ApplicationCommandModule<SlashCommandContext>
{
    private readonly IBlackjackService _blackjackService;

    public CommandModule(IBlackjackService blackjackService)
    {
        _blackjackService = blackjackService;
    }

    [SlashCommand("hourly", "Получить ежечасный бонус")]
    public async Task HourlyAsync()
    {
        var result = await _blackjackService.ClaimHourlyAsync(Context.User.Id);
        string response = result.IsSuccess
            ? $"✅ Вы получили **1000** монет! Ваш баланс: **{result.Value.Balance}**"
            : $"⏳ Бонус будет доступен <t:{result.Value.NextAvailable.ToUnixTimeSeconds()}:R>";

        // Инициализируем свойства
        var messageProps = new InteractionMessageProperties { Content = response };

        // Задаем Ephemeral только если это ошибка (результат неудачен)
        if (!result.IsSuccess)
            messageProps.Flags = MessageFlags.Ephemeral;

        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(messageProps));
    }

    [SlashCommand("bj", "Сыграть в блекджек")]
    public async Task BlackjackAsync(int bet)
    {
        var result = await _blackjackService.StartGameAsync(Context.User.Id, bet);
        if (!result.IsSuccess)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(
                new InteractionMessageProperties { Content = $"❌ {result.Error}", Flags = MessageFlags.Ephemeral }));
            return;
        }
        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(result.Value!.ToDiscordMessage()));
    }
}
