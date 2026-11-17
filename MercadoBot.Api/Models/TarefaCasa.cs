namespace MercadoBot.Api.Models;

public class TarefaCasa
{
    public int Id { get; set; }
    public long ChatId { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public DayOfWeek DiaSemana { get; set; }
}
