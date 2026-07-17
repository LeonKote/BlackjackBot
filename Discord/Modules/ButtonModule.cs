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

        // Получаем информацию о пользователе напрямую из Discord
        var targetUser = await Context.Client.Rest.GetUserAsync(userId);
        var player = await _playerRepo.GetOrCreateAsync(userId);

        await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(msg =>
        {
            msg.Embeds = [DiscordMapper.BuildProfileGeneralEmbed(targetUser, player)];
            msg.Components = DiscordMapper.BuildProfileComponents(userId);
        }));
    }

    [ComponentInteraction("profile_bj")]
    public async Task ProfileBjAsync(ulong userId)
    {
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;

        var targetUser = await Context.Client.Rest.GetUserAsync(userId);
        var player = await _playerRepo.GetOrCreateAsync(userId);

        await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(msg =>
        {
            msg.Embeds = [DiscordMapper.BuildProfileBjEmbed(targetUser, player)];
            msg.Components = DiscordMapper.BuildProfileComponents(userId);
        }));
    }

    [ComponentInteraction("profile_crash")]
    public async Task ProfileCrashAsync(ulong userId)
    {
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;

        var targetUser = await Context.Client.Rest.GetUserAsync(userId);
        var player = await _playerRepo.GetOrCreateAsync(userId);

        await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(msg =>
        {
            msg.Embeds = [DiscordMapper.BuildProfileCrashEmbed(targetUser, player)];
            msg.Components = DiscordMapper.BuildProfileComponents(userId);
        }));
    }

    [ComponentInteraction("profile_dice")]
    public async Task ProfileDiceAsync(ulong userId)
    {
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;

        var targetUser = await Context.Client.Rest.GetUserAsync(userId);
        var player = await _playerRepo.GetOrCreateAsync(userId);

        await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(msg =>
        {
            msg.Embeds = [DiscordMapper.BuildProfileDiceEmbed(targetUser, player)];
            msg.Components = DiscordMapper.BuildProfileComponents(userId);
        }));
    }

    [ComponentInteraction("mines_click")]
    public async Task MinesClickAsync(ulong userId, int tileIndex)
    {
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;
        if (Context.User.Id != userId) { await SendErrorAsync("Это не ваша игра!"); return; }

        var result = await _blackjackService.ClickMinesweeperAsync(userId, tileIndex);
        if (result.IsSuccess)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(msg =>
            {
                msg.Embeds = [DiscordMapper.BuildMinesweeperEmbed(result.Value!)];
                msg.Components = DiscordMapper.BuildMinesweeperComponents(result.Value!);
            }));
        }
        else await SendErrorAsync(result.Error);
    }

    [ComponentInteraction("mines_cashout")]
    public async Task MinesCashoutAsync(ulong userId)
    {
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;
        if (Context.User.Id != userId) { await SendErrorAsync("Это не ваша игра!"); return; }

        var result = await _blackjackService.CashoutMinesweeperAsync(userId);
        if (result.IsSuccess)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(msg =>
            {
                msg.Embeds = [DiscordMapper.BuildMinesweeperEmbed(result.Value!)];
                msg.Components = DiscordMapper.BuildMinesweeperComponents(result.Value!);
            }));
        }
        else await SendErrorAsync(result.Error);
    }

    [ComponentInteraction("profile_mines")]
    public async Task ProfileMinesAsync(ulong userId)
    {
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;

        var targetUser = await Context.Client.Rest.GetUserAsync(userId);
        var player = await _playerRepo.GetOrCreateAsync(userId);

        await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(msg =>
        {
            msg.Embeds = [DiscordMapper.BuildProfileMinesEmbed(targetUser, player)];
            msg.Components = DiscordMapper.BuildProfileComponents(userId);
        }));
    }

    [ComponentInteraction("hilo_guess")]
    public async Task HiloGuessAsync(ulong userId, string guess)
    {
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;
        if (Context.User.Id != userId) { await SendErrorAsync("Это не ваша игра!"); return; }

        var result = await _blackjackService.GuessHiloAsync(userId, guess);
        if (result.IsSuccess)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(msg =>
            {
                msg.Embeds = [DiscordMapper.BuildHiloEmbed(result.Value!)];
                msg.Components = DiscordMapper.BuildHiloComponents(result.Value!);
            }));
        }
        else await SendErrorAsync(result.Error);
    }

    [ComponentInteraction("hilo_cashout")]
    public async Task HiloCashoutAsync(ulong userId)
    {
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;
        if (Context.User.Id != userId) { await SendErrorAsync("Это не ваша игра!"); return; }

        var result = await _blackjackService.CashoutHiloAsync(userId);
        if (result.IsSuccess)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(msg =>
            {
                msg.Embeds = [DiscordMapper.BuildHiloEmbed(result.Value!)];
                msg.Components = DiscordMapper.BuildHiloComponents(result.Value!);
            }));
        }
        else await SendErrorAsync(result.Error);
    }

    [ComponentInteraction("profile_hilo")]
    public async Task ProfileHiloAsync(ulong userId)
    {
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;

        var targetUser = await Context.Client.Rest.GetUserAsync(userId);
        var player = await _playerRepo.GetOrCreateAsync(userId);

        await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(msg =>
        {
            msg.Embeds = [DiscordMapper.BuildProfileHiloEmbed(targetUser, player)];
            msg.Components = DiscordMapper.BuildProfileComponents(userId);
        }));
    }

    [ComponentInteraction("cancel_action")]
    public async Task CancelActionAsync(ulong userId)
    {
        if (Context.User.Id != userId) return;
        await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(msg => {
            msg.Content = "❌ Действие отменено.";
            msg.Embeds = []; msg.Components = [];
        }));
    }

    [ComponentInteraction("confirm_vip")]
    public async Task ConfirmVipAsync(ulong userId)
    {
        if (Context.User.Id != userId) return;
        var res = await _blackjackService.ConfirmVipAsync(userId);
        string text = res.IsSuccess ? "👑 **Вы успешно приобрели VIP статус на 30 дней!**" : $"❌ Ошибка: {res.Error}";
        await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(msg => { msg.Content = text; msg.Embeds = []; msg.Components = []; }));
    }

    [ComponentInteraction("confirm_booster")]
    public async Task ConfirmBoosterAsync(ulong userId)
    {
        if (Context.User.Id != userId) return;
        var res = await _blackjackService.ConfirmBoosterAsync(userId, false);
        string text = res.IsSuccess ? "🚀 **Обычный Бустер x2 куплен!**" : $"❌ Ошибка: {res.Error}";
        await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(msg => { msg.Content = text; msg.Embeds = []; msg.Components = []; }));
    }

    [ComponentInteraction("confirm_megabooster")]
    public async Task ConfirmMegaBoosterAsync(ulong userId)
    {
        if (Context.User.Id != userId) return;
        var res = await _blackjackService.ConfirmBoosterAsync(userId, true);
        string text = res.IsSuccess ? "🔥 **МЕГА-БУСТЕР x2 куплен!** Лимиты на прибыль сняты." : $"❌ Ошибка: {res.Error}";
        await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(msg => { msg.Content = text; msg.Embeds = []; msg.Components = []; }));
    }

    [ComponentInteraction("confirm_peek")]
    public async Task ConfirmPeekAsync(ulong userId)
    {
        if (Context.User.Id != userId) return;
        var res = await _blackjackService.ConfirmPeekAsync(userId);
        string text = res.IsSuccess ? $"👁️ **Следующие карты:** || {res.Value} ||" : $"❌ Ошибка: {res.Error}";
        await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(msg => { msg.Content = text; msg.Embeds = []; msg.Components = []; }));
    }

    [ComponentInteraction("confirm_refund")]
    public async Task ConfirmRefundAsync(ulong userId)
    {
        if (Context.User.Id != userId) return;
        var res = await _blackjackService.ConfirmRefundAsync(userId);
        string text = res.IsSuccess ? "✅ **Возврат успешно выполнен.** 50% от проигранной ставки зачислено обратно на ваш баланс." : $"❌ Ошибка: {res.Error}";
        await Context.Interaction.SendResponseAsync(InteractionCallback.ModifyMessage(msg => { msg.Content = text; msg.Embeds = []; msg.Components = []; }));
    }
}
