using BlackjackBot.Application.Interfaces;
using BlackjackBot.Application.Services;
using BlackjackBot.Domain.Entities;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace BlackjackBot.Discord;

public class ButtonModule : ComponentInteractionModule<ButtonInteractionContext>
{
    private readonly IBlackjackService _blackjackService;
    private readonly ChannelValidator _channelValidator;

    public ButtonModule(IBlackjackService blackjackService, ChannelValidator channelValidator)
    {
        _blackjackService = blackjackService;
        _channelValidator = channelValidator;
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

    private Task SendErrorAsync(string msg) => Context.Interaction.SendResponseAsync(InteractionCallback.Message(
        new InteractionMessageProperties { Content = $"❌ {msg}", Flags = MessageFlags.Ephemeral }));

    private Task UpdateMessageAsync(GameState game) => Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(msg => {
        var ui = game.ToDiscordMessage();
        msg.Embeds = ui.Embeds;
        msg.Components = ui.Components;
    }));
}
