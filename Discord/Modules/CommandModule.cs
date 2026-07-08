using BlackjackBot.Application.Interfaces;
using BlackjackBot.Discord.Services;
using BlackjackBot.Domain.Interfaces;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace BlackjackBot.Discord.Modules;

public class CommandModule : ApplicationCommandModule<SlashCommandContext>
{
    private readonly IBlackjackService _blackjackService;
    private readonly ChannelValidator _channelValidator;
    private readonly IPlayerRepository _playerRepo; // Добавляем репозиторий

    public CommandModule(IBlackjackService blackjackService, ChannelValidator channelValidator, IPlayerRepository playerRepo)
    {
        _blackjackService = blackjackService;
        _channelValidator = channelValidator;
        _playerRepo = playerRepo;
    }

    [SlashCommand("profile", "Профиль и статистика игрока")]
    public async Task ProfileAsync()
    {
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;

        var player = await _playerRepo.GetOrCreateAsync(Context.User.Id);
        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Embeds = [DiscordMapper.BuildProfileEmbed(Context.User, player)]
        }));
    }

    [SlashCommand("hourly", "Получить ежечасный бонус")]
    public async Task HourlyAsync()
    {
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;

        var result = await _blackjackService.ClaimHourlyAsync(Context.User.Id);

        string response = result.IsSuccess
            ? $"<@{Context.User.Id}> наклянчил косарь нищук"
            : $"⏳ Бонус будет доступен <t:{result.Value.NextAvailable.ToUnixTimeSeconds()}:R>";

        var messageProps = new InteractionMessageProperties { Content = response };
        if (!result.IsSuccess) messageProps.Flags = MessageFlags.Ephemeral; // Делаем скрытым только если кулдаун

        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(messageProps));
    }

    [SlashCommand("bj", "Сыграть в блекджек")]
    public async Task BlackjackAsync(int bet)
    {
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
            Content = $"<@{Context.User.Id}>", // Пинг игрока текстом вне embed-а
            Embeds = [DiscordMapper.BuildEmbed(game)],
            Components = DiscordMapper.BuildComponents(game)
        }));
    }
}
