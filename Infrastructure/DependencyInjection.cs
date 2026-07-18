using BlackjackBot.Domain.Interfaces;
using BlackjackBot.Infrastructure.Data;
using BlackjackBot.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BlackjackBot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(config.GetConnectionString("DefaultConnection")));

        services.AddSingleton<IPlayerRepository, PlayerRepository>();
        services.AddSingleton<IGameHistoryRepository, GameHistoryRepository>();

        return services;
    }

    public static async Task ApplyDatabaseMigrationsAsync(this IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
