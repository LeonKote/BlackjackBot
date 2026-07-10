using BlackjackBot.Application.Interfaces;
using BlackjackBot.Application.Services;
using BlackjackBot.Discord.Services;
using BlackjackBot.Domain.Entities;
using BlackjackBot.Domain.Interfaces;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace BlackjackBot.Discord.Modules;

public class ButtonModule : ComponentInteractionModule<ButtonInteractionContext>
{
    private readonly IBlackjackService _blackjackService;
    private readonly ChannelValidator _channelValidator;
    private readonly IPlayerRepository _playerRepo; // <-- Добавили

    public ButtonModule(IBlackjackService blackjackService, ChannelValidator channelValidator, IPlayerRepository playerRepo)
    {
        _blackjackService = blackjackService;
        _channelValidator = channelValidator;
        _playerRepo = playerRepo; // <-- Сохранили
    }

    [ComponentInteraction("bj_hit")]
    public async Task HitAsync(ulong userId)
    {
        // Защита кнопок от нажатий в других каналах (если сообщение было как-то переслано)
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;

        if (Context.User.Id != userId) { await SendErrorAsync("Это не ваша игра!"); return; }
        var result = await _blackjackService.HitAsync(userId);
        if (result.IsSuccess) await UpdateMessageAsync(result.Value!);
    }

    [ComponentInteraction("bj_stand")]
    public async Task StandAsync(ulong userId)
    {
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;

        if (Context.User.Id != userId) { await SendErrorAsync("Это не ваша игра!"); return; }
        var result = await _blackjackService.StandAsync(userId);
        if (result.IsSuccess) await UpdateMessageAsync(result.Value!);
    }

    [ComponentInteraction("bj_double")]
    public async Task DoubleAsync(ulong userId)
    {
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;
        if (Context.User.Id != userId) { await SendErrorAsync("Это не ваша игра!"); return; }

        var result = await _blackjackService.DoubleDownAsync(userId);
        if (result.IsSuccess) await UpdateMessageAsync(result.Value!);
        else await SendErrorAsync(result.Error);
    }

    [ComponentInteraction("bj_split")]
    public async Task SplitAsync(ulong userId)
    {
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;
        if (Context.User.Id != userId) { await SendErrorAsync("Это не ваша игра!"); return; }

        var result = await _blackjackService.SplitAsync(userId);
        if (result.IsSuccess) await UpdateMessageAsync(result.Value!);
        else await SendErrorAsync(result.Error);
    }

    private Task SendErrorAsync(string msg) => Context.Interaction.SendResponseAsync(InteractionCallback.Message(
        new InteractionMessageProperties { Content = $"❌ {msg}", Flags = MessageFlags.Ephemeral }));

    private Task UpdateMessageAsync(GameState game) => Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(msg =>
    {
        msg.Embeds = [DiscordMapper.BuildEmbed(game)];
        msg.Components = DiscordMapper.BuildComponents(game);
    }));

    [ComponentInteraction("profile_general")]
    public async Task ProfileGeneralAsync(ulong userId)
    {
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;
        if (Context.User.Id != userId) { await SendErrorAsync("Это чужой профиль!"); return; }

        var player = await _playerRepo.GetOrCreateAsync(userId);
        await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(msg =>
        {
            msg.Embeds = [DiscordMapper.BuildProfileGeneralEmbed(Context.User, player)];
            msg.Components = DiscordMapper.BuildProfileComponents(userId); // Оставляем кнопки
        }));
    }

    [ComponentInteraction("profile_bj")]
    public async Task ProfileBjAsync(ulong userId)
    {
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;
        if (Context.User.Id != userId) { await SendErrorAsync("Это чужой профиль!"); return; }

        var player = await _playerRepo.GetOrCreateAsync(userId);
        await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(msg =>
        {
            msg.Embeds = [DiscordMapper.BuildProfileBjEmbed(Context.User, player)];
            msg.Components = DiscordMapper.BuildProfileComponents(userId);
        }));
    }

    [ComponentInteraction("profile_crash")]
    public async Task ProfileCrashAsync(ulong userId)
    {
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;
        if (Context.User.Id != userId) { await SendErrorAsync("Это чужой профиль!"); return; }

        var player = await _playerRepo.GetOrCreateAsync(userId);
        await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(msg =>
        {
            msg.Embeds = [DiscordMapper.BuildProfileCrashEmbed(Context.User, player)];
            msg.Components = DiscordMapper.BuildProfileComponents(userId);
        }));
    }
}
