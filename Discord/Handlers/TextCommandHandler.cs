using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NetCord.Gateway;
using NetCord.Services.Commands;

namespace BlackjackBot.Discord.Handlers;

// Теперь это фоновый процесс (HostedService), который слушает события Discord
public class TextCommandHandler : BackgroundService
{
    private readonly GatewayClient _client;
    private readonly CommandService<CommandContext> _commandService;
    private readonly IServiceProvider _services;
    private readonly string _prefix;

    public TextCommandHandler(GatewayClient client, CommandService<CommandContext> commandService, IServiceProvider services, IConfiguration config)
    {
        _client = client;
        _commandService = commandService;
        _services = services;
        _prefix = config["Discord:Prefix"] ?? "!";
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Подписываемся на событие создания сообщения в чате
        _client.MessageCreate += HandleMessageAsync;
        return Task.CompletedTask;
    }

    private async ValueTask HandleMessageAsync(Message message)
    {
        if (message.Author.IsBot || !message.Content.StartsWith(_prefix)) return;

        var context = new CommandContext(message, _client);
        await _commandService.ExecuteAsync(_prefix.Length, context, _services);
    }
}
