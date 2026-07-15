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

        bool hasBj = _sessionManager.TryGetGame(Context.Message.Author.Id, out var bjGame);
        bool hasMines = _sessionManager.TryGetMinesGame(Context.Message.Author.Id, out var minesGame);
        bool hasHilo = _sessionManager.TryGetHiloGame(Context.Message.Author.Id, out var hiloGame); // Новое

        if (!hasBj && !hasMines && !hasHilo)
        {
            await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties
            {
                Content = "❌ У вас нет активной игры в данный момент.",
                MessageReference = MessageReferenceProperties.Reply(Context.Message.Id)
            });
            return;
        }

        var reply = new MessageProperties
        {
            Content = $"<@{Context.Message.Author.Id}> (Игра восстановлена)",
            MessageReference = MessageReferenceProperties.Reply(Context.Message.Id)
        };

        if (hasBj)
        {
            reply.Embeds = [DiscordMapper.BuildEmbed(bjGame!)];
            reply.Components = DiscordMapper.BuildComponents(bjGame!);
        }
        else if (hasMines)
        {
            reply.Embeds = [DiscordMapper.BuildMinesweeperEmbed(minesGame!)];
            reply.Components = DiscordMapper.BuildMinesweeperComponents(minesGame!);
        }
        else if (hasHilo)
        {
            reply.Embeds = [DiscordMapper.BuildHiloEmbed(hiloGame!)];
            reply.Components = DiscordMapper.BuildHiloComponents(hiloGame!);
        }

        await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, reply);
    }

    [Command("profile", "stats")]
    public async Task ProfileAsync([CommandParameter(Remainder = true)] string? arg = null)
    {
        if (!_channelValidator.IsAllowed(Context.Message.ChannelId)) return;

        User targetUser = Context.Message.Author;

        // Если после команды что-то написали (например пинг или ID)
        if (!string.IsNullOrWhiteSpace(arg))
        {
            // Ищем в тексте последовательность цифр длиной от 17 до 20 (это стандартный ID Discord)
            // Он идеально достанет ID как из текста "123456789...", так и из пинга "<@123456789...>"
            var match = System.Text.RegularExpressions.Regex.Match(arg, @"\d{17,20}");

            if (match.Success && ulong.TryParse(match.Value, out var userId))
            {
                try
                {
                    targetUser = await Context.Client.Rest.GetUserAsync(userId);
                }
                catch
                {
                    await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties
                    {
                        Content = "❌ Пользователь не найден. Возможно, бот его не видит.",
                        MessageReference = MessageReferenceProperties.Reply(Context.Message.Id)
                    });
                    return;
                }
            }
            else
            {
                await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties
                {
                    Content = "❌ Укажите корректный пинг пользователя (через @) или его ID.",
                    MessageReference = MessageReferenceProperties.Reply(Context.Message.Id)
                });
                return;
            }
        }

        var player = await _playerRepo.GetOrCreateAsync(targetUser.Id);
        await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties
        {
            Embeds = [DiscordMapper.BuildProfileGeneralEmbed(targetUser, player)],
            Components = DiscordMapper.BuildProfileComponents(targetUser.Id),
            MessageReference = MessageReferenceProperties.Reply(Context.Message.Id)
        });
    }

    [Command("hourly")]
    public async Task HourlyAsync()
    {
        if (!_channelValidator.IsAllowed(Context.Message.ChannelId)) return;

        var result = await _blackjackService.ClaimHourlyAsync(Context.Message.Author.Id);

        string response = result.IsSuccess
            ? DiscordMapper.GetRandomHourlyMessage(Context.Message.Author.Id)
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

    [Command("hilo")]
    public async Task HiloAsync(int bet)
    {
        if (!_channelValidator.IsAllowed(Context.Message.ChannelId)) return;

        var result = await _blackjackService.StartHiloAsync(Context.Message.Author.Id, bet);
        if (!result.IsSuccess)
        {
            await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties { Content = $"❌ {result.Error}", MessageReference = MessageReferenceProperties.Reply(Context.Message.Id) });
            return;
        }

        var game = result.Value!;
        await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties
        {
            Content = $"<@{Context.Message.Author.Id}>",
            Embeds = [DiscordMapper.BuildHiloEmbed(game)],
            Components = DiscordMapper.BuildHiloComponents(game),
            MessageReference = MessageReferenceProperties.Reply(Context.Message.Id)
        });
    }

    [Command("daily")]
    public async Task DailyAsync()
    {
        if (!_channelValidator.IsAllowed(Context.Message.ChannelId)) return;

        var result = await _blackjackService.ClaimDailyAsync(Context.Message.Author.Id);

        string response = result.IsSuccess
            ? DiscordMapper.GetRandomDailyMessage(Context.Message.Author.Id)
            : $"⏳ Ежедневный бонус будет доступен <t:{result.Value.NextAvailable.ToUnixTimeSeconds()}:R>";

        await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties
        {
            Content = response,
            MessageReference = MessageReferenceProperties.Reply(Context.Message.Id)
        });
    }

    [Command("top", "leaderboard")]
    public async Task TopAsync()
    {
        if (!_channelValidator.IsAllowed(Context.Message.ChannelId)) return;

        var topPlayers = await _blackjackService.GetTopPlayersAsync(10);

        await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties
        {
            Embeds = [DiscordMapper.BuildLeaderboardEmbed(topPlayers)],
            MessageReference = MessageReferenceProperties.Reply(Context.Message.Id)
        });
    }

    [Command("shop")]
    public async Task ShopAsync()
    {
        await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties { Embeds = [DiscordMapper.BuildShopEmbed()], MessageReference = MessageReferenceProperties.Reply(Context.Message.Id) });
    }

    [Command("vip")]
    public async Task VipAsync()
    {
        var res = await _blackjackService.PreVipCheckAsync(Context.Message.Author.Id);
        if (!res.IsSuccess) { await SendErrorAsync(res.Error); return; }

        await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties
        {
            Embeds = [DiscordMapper.BuildConfirmationEmbed("Покупка VIP", $"Вы собираетесь приобрести VIP статус на 30 дней.\nЦена: **{res.Value} 💎**")],
            Components = DiscordMapper.BuildConfirmationComponents("vip", Context.Message.Author.Id),
            MessageReference = MessageReferenceProperties.Reply(Context.Message.Id)
        });
    }

    [Command("booster")]
    public async Task BoosterAsync(string type = "")
    {
        bool isMega = type.ToLower() == "mega";
        var res = await _blackjackService.PreBoosterCheckAsync(Context.Message.Author.Id, isMega);
        if (!res.IsSuccess) { await SendErrorAsync(res.Error); return; }

        string title = isMega ? "Мега-Бустер x2" : "Обычный Бустер x2";
        string desc = isMega
            ? $"Мега-бустер удвоит выигрыш вашей СЛЕДУЮЩЕЙ игры **без ограничений**.\nЦена: **{res.Value} 💎**"
            : $"Бустер удвоит выигрыш вашей СЛЕДУЮЩЕЙ игры (максимум до 50,000 монет).\nДля покупки Мега-бустера напишите `!booster mega`.\nЦена: **{res.Value} 💎**";

        string actionData = isMega ? "megabooster" : "booster";

        await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties
        {
            Embeds = [DiscordMapper.BuildConfirmationEmbed(title, desc)],
            Components = DiscordMapper.BuildConfirmationComponents(actionData, Context.Message.Author.Id),
            MessageReference = MessageReferenceProperties.Reply(Context.Message.Id)
        });
    }

    [Command("peek")]
    public async Task PeekAsync()
    {
        var res = await _blackjackService.PrePeekCheckAsync(Context.Message.Author.Id);
        if (!res.IsSuccess) { await SendErrorAsync(res.Error); return; }

        await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties
        {
            Embeds = [DiscordMapper.BuildConfirmationEmbed("Просмотр карт", $"Вы увидите 2 следующие карты в текущей раздаче.\nЦена: **{res.Value} 💎**")],
            Components = DiscordMapper.BuildConfirmationComponents("peek", Context.Message.Author.Id),
            MessageReference = MessageReferenceProperties.Reply(Context.Message.Id)
        });
    }

    [Command("refund", "возврат")]
    public async Task RefundAsync()
    {
        var res = await _blackjackService.PreRefundCheckAsync(Context.Message.Author.Id);
        if (!res.IsSuccess) { await SendErrorAsync(res.Error); return; }

        await Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties
        {
            Embeds = [DiscordMapper.BuildConfirmationEmbed("Возврат ставки", $"Вы вернете на баланс сумму своей последней проигранной ставки.\nЦена: **{res.Value} 💎**")],
            Components = DiscordMapper.BuildConfirmationComponents("refund", Context.Message.Author.Id),
            MessageReference = MessageReferenceProperties.Reply(Context.Message.Id)
        });
    }

    private Task SendErrorAsync(string error) => Context.Client.Rest.SendMessageAsync(Context.Message.ChannelId, new MessageProperties { Content = $"❌ {error}", MessageReference = MessageReferenceProperties.Reply(Context.Message.Id) });
}
