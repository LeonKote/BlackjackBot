using BlackjackBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlackjackBot.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public DbSet<Player> Players { get; set; }
    public DbSet<GameHistory> GameHistories { get; set; } // Новая таблица

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>().HasKey(p => p.Id);
        modelBuilder.Entity<GameHistory>().HasKey(h => h.Id); // Ключ для истории
    }
}
