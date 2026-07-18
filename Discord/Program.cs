using BlackjackBot.Application;
using BlackjackBot.Infrastructure;
using Microsoft.Extensions.Hosting;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.Commands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.Commands;
using NetCord.Services.ComponentInteractions;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
     .AddDiscordGateway(options =>
     {
         options.Intents = GatewayIntents.GuildMessages | GatewayIntents.DirectMessages | GatewayIntents.MessageContent;
     })
    .AddCommands<CommandContext>()
    .AddApplicationCommands<SlashCommandInteraction, SlashCommandContext>()
    .AddComponentInteractions<ButtonInteraction, ButtonInteractionContext>();

var host = builder.Build();

await host.Services.ApplyDatabaseMigrationsAsync();

host.AddModules(typeof(Program).Assembly);
await host.RunAsync();
