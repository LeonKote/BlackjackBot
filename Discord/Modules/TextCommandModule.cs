using BlackjackBot.Application.Interfaces;
using BlackjackBot.Discord.Services;
using BlackjackBot.Domain.Interfaces;
using NetCord;
using NetCord.Rest;
using NetCord.Services.Commands;

namespace BlackjackBot.Discord.Modules;

public class TextCommandModule : CommandModule<CommandContext>
{
    private readonly IBlackjackService _blackjackService;
    private readonly ChannelValidator _channelValidator;
    private readonly IPlayerRepository _playerRepo;

    public TextCommandModule(IBlackjackService blackjackService, ChannelValidator channelValidator, IPlayerRepository playerRepo)
    {
        _blackjackService = blackjackService;
        _channelValidator = channelValidator;
        _playerRepo = playerRepo;
    }

    [Command("profile", "stats")]
    public async Task ProfileAsync()
    {
        if (!_channelValidator.IsAllowed(Context.Message.ChannelId)) return;

        var player = await _playerRepo.GetOrCreateAsync(Context.Message.Author.Id);
        await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties
        {
            Embeds = [DiscordMapper.BuildProfileEmbed(Context.Message.Author, player)],
            MessageReference = MessageReferenceProperties.Reply(Context.Message.Id)
        });
    }

    [Command("hourly")]
    public async Task HourlyAsync()
    {
        if (!_channelValidator.IsAllowed(Context.Message.ChannelId)) return;

        var result = await _blackjackService.ClaimHourlyAsync(Context.Message.Author.Id);

        string response = result.IsSuccess
            ? $"<@{Context.Message.Author.Id}> наклянчил косарь нищук"
            : $"⏳ Бонус будет доступен <t:{result.Value.NextAvailable.ToUnixTimeSeconds()}:R>";

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
            Content = $"<@{Context.Message.Author.Id}>", // Пинг вне embed-а
            Embeds = [DiscordMapper.BuildEmbed(game)],
            Components = DiscordMapper.BuildComponents(game),
            MessageReference = MessageReferenceProperties.Reply(Context.Message.Id)
        };

        await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, reply);
    }
}
