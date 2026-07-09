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
    private readonly IPlayerRepository _playerRepo;
    private readonly IGameSessionManager _sessionManager; // <-- Добавили менеджер сессий

    public CommandModule(
        IBlackjackService blackjackService,
        ChannelValidator channelValidator,
        IPlayerRepository playerRepo,
        IGameSessionManager sessionManager)
    {
        _blackjackService = blackjackService;
        _channelValidator = channelValidator;
        _playerRepo = playerRepo;
        _sessionManager = sessionManager;
    }

    [SlashCommand("recover", "Восстановить игру, если сообщение удалилось")]
    public async Task RecoverAsync()
    {
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;

        if (!_sessionManager.TryGetGame(Context.User.Id, out var game) || game is null)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(
                new InteractionMessageProperties { Content = "❌ У вас нет активной игры в данный момент.", Flags = MessageFlags.Ephemeral }));
            return;
        }

        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Content = $"<@{Context.User.Id}> (Игра восстановлена)",
            Embeds = [DiscordMapper.BuildEmbed(game)],
            Components = DiscordMapper.BuildComponents(game)
        }));
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
        if (!result.IsSuccess) messageProps.Flags = MessageFlags.Ephemeral;

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
            Content = $"<@{Context.User.Id}>",
            Embeds = [DiscordMapper.BuildEmbed(game)],
            Components = DiscordMapper.BuildComponents(game)
        }));
    }

    [SlashCommand("seed", "Изменить свой Client Seed для проверки честности")]
    public async Task SeedAsync(string newSeed)
    {
        // Проверка канала
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;

        var result = await _blackjackService.ChangeSeedAsync(Context.User.Id, newSeed);

        string text = result.IsSuccess
            ? $"✅ Ваш Client Seed успешно изменен на `{newSeed}`."
            : $"❌ {result.Error}";

        var messageProps = new InteractionMessageProperties { Content = text };

        // Ошибки выводим скрытым сообщением
        if (!result.IsSuccess)
            messageProps.Flags = MessageFlags.Ephemeral;

        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(messageProps));
    }

    [SlashCommand("proof", "Проверить честность сыгранной игры по её ID")]
    public async Task ProofAsync(long gameId)
    {
        // Проверка канала
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;

        var result = await _blackjackService.GetGameProofAsync(gameId);

        if (!result.IsSuccess)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(
                new InteractionMessageProperties { Content = $"❌ {result.Error}", Flags = MessageFlags.Ephemeral }));
            return;
        }

        // Если игра найдена, выводим Embed с доказательством
        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Embeds = [DiscordMapper.BuildProofEmbed(result.Value!)]
        }));
    }
}
