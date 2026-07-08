using BlackjackBot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlackjackBot.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public DbSet<Player> Players { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>().HasKey(p => p.Id); // Fluent API вместо атрибутов в Domain
    }
}
