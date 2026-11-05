using MercadoBot.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MercadoBot.Api.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<ItemCompra> ItensCompra => Set<ItemCompra>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ItemCompra>(entity =>
        {
            entity.HasKey(item => item.Id);

            entity.Property(item => item.Nome)
                .IsRequired()
                .HasMaxLength(150);

            entity.HasIndex(item => new
            {
                item.ChatId,
                item.Ano,
                item.Mes
            });
        });
    }
}