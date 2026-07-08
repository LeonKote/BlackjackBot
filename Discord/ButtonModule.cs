using BlackjackBot.Application.Interfaces;
using BlackjackBot.Domain.Entities;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

namespace BlackjackBot.Discord;

public class ButtonModule : ComponentInteractionModule<ButtonInteractionContext>
{
    private readonly IBlackjackService _blackjackService;

    public ButtonModule(IBlackjackService blackjackService)
    {
        _blackjackService = blackjackService;
    }

    [ComponentInteraction("bj_hit")]
    public async Task HitAsync(ulong userId)
    {
        if (Context.User.Id != userId) { await SendErrorAsync("Это не ваша игра!"); return; }
        var result = await _blackjackService.HitAsync(userId);
        if (result.IsSuccess) await UpdateMessageAsync(result.Value!);
    }

    [ComponentInteraction("bj_stand")]
    public async Task StandAsync(ulong userId)
    {
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
