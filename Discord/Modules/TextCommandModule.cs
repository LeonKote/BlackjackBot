using BlackjackBot.Application.Interfaces;
using BlackjackBot.Discord.Services;
using NetCord;
using NetCord.Rest;
using NetCord.Services.Commands;

namespace BlackjackBot.Discord.Modules;

public class TextCommandModule : CommandModule<CommandContext>
{
    private readonly IBlackjackService _blackjackService;
    private readonly ChannelValidator _channelValidator;

    public TextCommandModule(IBlackjackService blackjackService, ChannelValidator channelValidator)
    {
        _blackjackService = blackjackService;
        _channelValidator = channelValidator;
    }

    [Command("hourly")]
    public async Task HourlyAsync()
    {
        if (!_channelValidator.IsAllowed(Context.Message.ChannelId)) return;

        var result = await _blackjackService.ClaimHourlyAsync(Context.Message.Author.Id);
        string response = result.IsSuccess
            ? $"✅ Вы получили **1000** монет! Ваш баланс: **{result.Value.Balance}**"
            : $"⏳ Бонус будет доступен <t:{result.Value.NextAvailable.ToUnixTimeSeconds()}:R>";

        // Ответ с упоминанием сообщения игрока (ReplyToMessageId)
        await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties
        {
            Content = response,
            MessageReference = MessageReferenceProperties.Reply(Context.Message.Id)
        });
    }

    [Command("bj")]
    public async Task BlackjackAsync(int bet)
    {
        if (!_channelValidator.IsAllowed(Context.Message.ChannelId)) return;

        var result = await _blackjackService.StartGameAsync(Context.Message.Author.Id, bet);
        if (!result.IsSuccess)
        {
            await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties { Content = $"❌ {result.Error}", MessageReference = MessageReferenceProperties.Reply(Context.Message.Id) });
            return;
        }

        var game = result.Value!;
        var reply = new MessageProperties
        {
            Embeds = [DiscordMapper.BuildEmbed(game)],
            Components = DiscordMapper.BuildComponents(game),
            MessageReference = MessageReferenceProperties.Reply(Context.Message.Id)
        };

        await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, reply);
    }
}
