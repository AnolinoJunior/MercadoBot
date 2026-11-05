namespace MercadoBot.Api.Models;

public class ItemCompra
{
    public int Id { get; set; }

    public long ChatId { get; set; }

    public string Nome { get; set; } = string.Empty;

    public int Quantidade { get; set; } = 1;

    public bool Comprado { get; set; }

    public int Mes { get; set; }

    public int Ano { get; set; }

    public DateTime? CompradoEm { get; set; }
}