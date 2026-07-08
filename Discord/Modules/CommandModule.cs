using BlackjackBot.Application.Interfaces;
using BlackjackBot.Discord.Services;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace BlackjackBot.Discord.Modules;

public class CommandModule : ApplicationCommandModule<SlashCommandContext>
{
    private readonly IBlackjackService _blackjackService;
    private readonly ChannelValidator _channelValidator;

    public CommandModule(IBlackjackService blackjackService, ChannelValidator channelValidator)
    {
        this._blackjackService = blackjackService;
        this._channelValidator = channelValidator;
    }

    [SlashCommand("hourly", "Получить ежечасный бонус")]
    public async Task HourlyAsync()
    {
        // Проверка канала
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;

        var result = await _blackjackService.ClaimHourlyAsync(Context.User.Id);
        string response = result.IsSuccess
            ? $"✅ Вы получили **1000** монет! Ваш баланс: **{result.Value.Balance}**"
            : $"⏳ Бонус будет доступен <t:{result.Value.NextAvailable.ToUnixTimeSeconds()}:R>";

        var messageProps = new InteractionMessageProperties { Content = response };
        if (!result.IsSuccess) messageProps.Flags = MessageFlags.Ephemeral;

        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(messageProps));
    }

    [SlashCommand("bj", "Сыграть в блекджек")]
    public async Task BlackjackAsync(int bet)
    {
        // Проверка канала
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;

        var result = await _blackjackService.StartGameAsync(Context.User.Id, bet);
        if (!result.IsSuccess)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(
                new InteractionMessageProperties { Content = $"❌ {result.Error}", Flags = MessageFlags.Ephemeral }));
            return;
        }
        var game = result.Value!;
        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Embeds = [DiscordMapper.BuildEmbed(game)],
            Components = DiscordMapper.BuildComponents(game)
        }));
    }
}
