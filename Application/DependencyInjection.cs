using BlackjackBot.Application.Interfaces;
using BlackjackBot.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BlackjackBot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IGameSessionManager, GameSessionManager>();
        services.AddSingleton<IBlackjackService, BlackjackService>();

        return services;
    }
}
