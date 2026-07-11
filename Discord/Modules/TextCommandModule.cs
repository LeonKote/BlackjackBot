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
    private readonly IGameSessionManager _sessionManager; // <-- Добавили менеджер сессий

    public TextCommandModule(
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

    [Command("recover")]
    public async Task RecoverAsync()
    {
        if (!_channelValidator.IsAllowed(Context.Message.ChannelId)) return;

        // Проверяем, есть ли активная игра
        if (!_sessionManager.TryGetGame(Context.Message.Author.Id, out var game) || game is null)
        {
            await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties
            {
                Content = "❌ У вас нет активной игры в данный момент.",
                MessageReference = MessageReferenceProperties.Reply(Context.Message.Id)
            });
            return;
        }

        // Отправляем текущее состояние игры заново
        var reply = new MessageProperties
        {
            Content = $"<@{Context.Message.Author.Id}> (Игра восстановлена)",
            Embeds = [DiscordMapper.BuildEmbed(game)],
            Components = DiscordMapper.BuildComponents(game),
            MessageReference = MessageReferenceProperties.Reply(Context.Message.Id)
        };

        await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, reply);
    }

    [Command("profile", "stats")]
    public async Task ProfileAsync()
    {
        if (!_channelValidator.IsAllowed(Context.Message.ChannelId)) return;

        var player = await _playerRepo.GetOrCreateAsync(Context.Message.Author.Id);
        await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties
        {
            Embeds = [DiscordMapper.BuildProfileGeneralEmbed(Context.Message.Author, player)],
            Components = DiscordMapper.BuildProfileComponents(Context.Message.Author.Id), // Выводим кнопки
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
            Content = $"<@{Context.Message.Author.Id}>",
            Embeds = [DiscordMapper.BuildEmbed(game)],
            Components = DiscordMapper.BuildComponents(game),
            MessageReference = MessageReferenceProperties.Reply(Context.Message.Id)
        };

        await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, reply);
    }

    [Command("seed")]
    public async Task SeedAsync([CommandParameter(Remainder = true)] string newSeed)
    {
        if (!_channelValidator.IsAllowed(Context.Message.ChannelId)) return;

        var result = await _blackjackService.ChangeSeedAsync(Context.Message.Author.Id, newSeed);
        string text = result.IsSuccess ? $"✅ Ваш Client Seed успешно изменен на `{newSeed}`." : $"❌ {result.Error}"; // Изменено тут

        await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties { Content = text, MessageReference = MessageReferenceProperties.Reply(Context.Message.Id) });
    }

    [Command("proof")]
    public async Task ProofAsync(long gameId)
    {
        if (!_channelValidator.IsAllowed(Context.Message.ChannelId)) return;

        var result = await _blackjackService.GetGameProofAsync(gameId);
        if (!result.IsSuccess)
        {
            await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties { Content = $"❌ {result.Error}", MessageReference = MessageReferenceProperties.Reply(Context.Message.Id) });
            return;
        }

        await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties
        {
            Embeds = [DiscordMapper.BuildProofEmbed(result.Value!)],
            MessageReference = MessageReferenceProperties.Reply(Context.Message.Id)
        });
    }

    [Command("crash")]
    public async Task CrashAsync(int bet, double target)
    {
        if (!_channelValidator.IsAllowed(Context.Message.ChannelId)) return;

        var result = await _blackjackService.PlayCrashAsync(Context.Message.Author.Id, bet, target);
        if (!result.IsSuccess)
        {
            await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties { Content = $"❌ {result.Error}", MessageReference = MessageReferenceProperties.Reply(Context.Message.Id) });
            return;
        }

        var reply = new MessageProperties
        {
            Content = $"<@{Context.Message.Author.Id}>",
            Embeds = [DiscordMapper.BuildCrashEmbed(result.Value!)],
            MessageReference = MessageReferenceProperties.Reply(Context.Message.Id)
        };

        await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, reply);
    }

    [Command("help")]
    public async Task HelpAsync()
    {
        if (!_channelValidator.IsAllowed(Context.Message.ChannelId)) return;

        await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties
        {
            Embeds = [DiscordMapper.BuildHelpEmbed()],
            MessageReference = MessageReferenceProperties.Reply(Context.Message.Id)
        });
    }

    [Command("nextseed")]
    public async Task NextSeedAsync()
    {
        if (!_channelValidator.IsAllowed(Context.Message.ChannelId)) return;

        var result = await _blackjackService.GetNextSeedInfoAsync(Context.Message.Author.Id);

        await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties
        {
            Embeds = [DiscordMapper.BuildNextSeedEmbed(result.Value.ServerSeedHash, result.Value.ClientSeed)],
            MessageReference = MessageReferenceProperties.Reply(Context.Message.Id)
        });
    }

    [Command("dice")]
    public async Task DiceAsync(int bet, int min, int max)
    {
        if (!_channelValidator.IsAllowed(Context.Message.ChannelId)) return;

        var result = await _blackjackService.PlayDiceAsync(Context.Message.Author.Id, bet, min, max);
        if (!result.IsSuccess)
        {
            await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties { Content = $"❌ {result.Error}", MessageReference = MessageReferenceProperties.Reply(Context.Message.Id) });
            return;
        }

        var reply = new MessageProperties
        {
            Content = $"<@{Context.Message.Author.Id}>",
            Embeds = [DiscordMapper.BuildDiceEmbed(result.Value!)],
            MessageReference = MessageReferenceProperties.Reply(Context.Message.Id)
        };

        await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, reply);
    }

    [Command("mines")]
    public async Task MinesAsync(int bet, int minesCount)
    {
        if (!_channelValidator.IsAllowed(Context.Message.ChannelId)) return;

        var result = await _blackjackService.StartMinesweeperAsync(Context.Message.Author.Id, bet, minesCount);
        if (!result.IsSuccess)
        {
            await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties { Content = $"❌ {result.Error}", MessageReference = MessageReferenceProperties.Reply(Context.Message.Id) });
            return;
        }

        var game = result.Value!;
        await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties
        {
            Content = $"<@{Context.Message.Author.Id}>",
            Embeds = [DiscordMapper.BuildMinesweeperEmbed(game)],
            Components = DiscordMapper.BuildMinesweeperComponents(game),
            MessageReference = MessageReferenceProperties.Reply(Context.Message.Id)
        });
    }
}
