using BlackjackBot.Application.Interfaces;
using BlackjackBot.Discord.Services;
using BlackjackBot.Domain.Interfaces;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;

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

        bool hasBj = _sessionManager.TryGetGame(Context.User.Id, out var bjGame);
        bool hasMines = _sessionManager.TryGetMinesGame(Context.User.Id, out var minesGame);
        bool hasHilo = _sessionManager.TryGetHiloGame(Context.User.Id, out var hiloGame);

        if (!hasBj && !hasMines && !hasHilo)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(
                new InteractionMessageProperties { Content = "❌ У вас нет активной игры в данный момент.", Flags = MessageFlags.Ephemeral }));
            return;
        }

        var props = new InteractionMessageProperties
        {
            Content = $"<@{Context.User.Id}> (Игра восстановлена)"
        };

        if (hasBj)
        {
            props.Embeds = [DiscordMapper.BuildEmbed(bjGame!)];
            props.Components = DiscordMapper.BuildComponents(bjGame!);
        }
        else if (hasMines)
        {
            props.Embeds = [DiscordMapper.BuildMinesweeperEmbed(minesGame!)];
            props.Components = DiscordMapper.BuildMinesweeperComponents(minesGame!);
        }
        else if (hasHilo)
        {
            props.Embeds = [DiscordMapper.BuildHiloEmbed(hiloGame!)];
            props.Components = DiscordMapper.BuildHiloComponents(hiloGame!);
        }

        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(props));
    }

    [SlashCommand("profile", "Профиль и статистика игрока")]
    public async Task ProfileAsync()
    {
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;

        var player = await _playerRepo.GetOrCreateAsync(Context.User.Id);
        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Embeds = [DiscordMapper.BuildProfileGeneralEmbed(Context.User, player)],
            Components = DiscordMapper.BuildProfileComponents(Context.User.Id) // Выводим кнопки
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

    [SlashCommand("crash", "Сыграть в Краш")]
    public async Task CrashAsync(int bet, double target)
    {
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;

        var result = await _blackjackService.PlayCrashAsync(Context.User.Id, bet, target);
        if (!result.IsSuccess)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(
                new InteractionMessageProperties { Content = $"❌ {result.Error}", Flags = MessageFlags.Ephemeral }));
            return;
        }

        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Content = $"<@{Context.User.Id}>",
            Embeds = [DiscordMapper.BuildCrashEmbed(result.Value!)]
        }));
    }

    [SlashCommand("help", "Показать список всех команд и правил")]
    public async Task HelpAsync()
    {
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;

        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Embeds = [DiscordMapper.BuildHelpEmbed()]
        }));
    }

    [SlashCommand("nextseed", "Посмотреть хеш сервера для вашей следующей игры")]
    public async Task NextSeedAsync()
    {
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;

        var result = await _blackjackService.GetNextSeedInfoAsync(Context.User.Id);

        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Embeds = [DiscordMapper.BuildNextSeedEmbed(result.Value.ServerSeedHash, result.Value.ClientSeed)]
        }));
    }

    [SlashCommand("dice", "Сыграть в Дайс (Кости)")]
    public async Task DiceAsync(int bet, int min, int max)
    {
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;

        var result = await _blackjackService.PlayDiceAsync(Context.User.Id, bet, min, max);
        if (!result.IsSuccess)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(
                new InteractionMessageProperties { Content = $"❌ {result.Error}", Flags = MessageFlags.Ephemeral }));
            return;
        }

        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Content = $"<@{Context.User.Id}>",
            Embeds = [DiscordMapper.BuildDiceEmbed(result.Value!)]
        }));
    }

    [SlashCommand("mines", "Сыграть в Сапёра")]
    public async Task MinesAsync(int bet, int minesCount)
    {
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;

        var result = await _blackjackService.StartMinesweeperAsync(Context.User.Id, bet, minesCount);
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
            Embeds = [DiscordMapper.BuildMinesweeperEmbed(game)],
            Components = DiscordMapper.BuildMinesweeperComponents(game)
        }));
    }

    [SlashCommand("hilo", "Сыграть в Выше-Ниже")]
    public async Task HiloAsync(int bet)
    {
        if (!_channelValidator.IsAllowed(Context.Interaction.Channel.Id)) return;

        var result = await _blackjackService.StartHiloAsync(Context.User.Id, bet);
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
            Embeds = [DiscordMapper.BuildHiloEmbed(game)],
            Components = DiscordMapper.BuildHiloComponents(game)
        }));
    }
}
