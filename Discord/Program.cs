using BlackjackBot.Application.Interfaces;
using BlackjackBot.Application.Services;
using BlackjackBot.Discord.Handlers;
using BlackjackBot.Discord.Services;
using BlackjackBot.Domain.Interfaces;
using BlackjackBot.Infrastructure.Data;
using BlackjackBot.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetCord;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.Commands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;
using NetCord.Services.ComponentInteractions;

var builder = Host.CreateApplicationBuilder(args);

// Регистрация Инфраструктуры
var connString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContextFactory<AppDbContext>(opt => opt.UseNpgsql(connString));
builder.Services.AddSingleton<IPlayerRepository, PlayerRepository>();

// Регистрация Приложения
builder.Services.AddSingleton<IGameSessionManager, GameSessionManager>();
builder.Services.AddSingleton<IBlackjackService, BlackjackService>();
builder.Services.AddSingleton<ChannelValidator>();

// Регистрация Представления (Discord)
builder.Services
    .AddDiscordGateway()
    .AddCommands<CommandContext>()
    .AddApplicationCommands<SlashCommandInteraction, SlashCommandContext>()
    .AddComponentInteractions<ButtonInteraction, ButtonInteractionContext>();

builder.Services.AddHostedService<TextCommandHandler>();

var host = builder.Build();

// Авто-миграции
using (var scope = host.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

host.AddModules(typeof(Program).Assembly);
await host.RunAsync();
