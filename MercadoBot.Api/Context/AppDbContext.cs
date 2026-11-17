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
    public DbSet<TarefaCasa> TarefasCasa => Set<TarefaCasa>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TarefaCasa>(entity =>
        {
            entity.HasKey(tarefa => tarefa.Id);
            entity.Property(tarefa => tarefa.Descricao).IsRequired().HasMaxLength(300);
            entity.HasIndex(tarefa => new { tarefa.ChatId, tarefa.DiaSemana });
        });

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