using System.Globalization;
using System.Text.RegularExpressions;
using MercadoBot.Api.Context;
using MercadoBot.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace MercadoBot.Api.Services;

public class ListaComprasService
{
    private readonly AppDbContext _context;

    private static readonly TimeZoneInfo FusoBrasilia =
        TimeZoneInfo.FindSystemTimeZoneById(
            "E. South America Standard Time");

    public ListaComprasService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<string> ProcessarMensagemAsync(
        long chatId,
        string texto,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return string.Empty;
        }

        var partes = texto.Trim().Split(
            new[] { ' ', '\t', '\r', '\n' },
            2,
            StringSplitOptions.RemoveEmptyEntries);

        var partesComando = partes[0].Split('@', 2);

        if (partesComando.Length == 2 &&
            !partesComando[1].Equals(
                "Pouco_bot",
                StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var comando = partesComando[0].ToLowerInvariant();

        if (!comando.StartsWith('/'))
        {
            return string.Empty;
        }

        var agoraUtc = DateTime.UtcNow;

        var agoraBrasilia = TimeZoneInfo.ConvertTimeFromUtc(
            agoraUtc,
            FusoBrasilia);

        await LimparHistoricoAntigoAsync(
            chatId,
            agoraBrasilia,
            cancellationToken);

        switch (comando)
        {
            case "/start":
            case "/ajuda":
                {
                    return "Sua lista de compras 🛒\n\n" +
                           "Use os botões para adicionar produtos, " +
                           "consultar a lista e ver o histórico.\n\n" +
                           "Para informar quantidades:\n" +
                           "2 bolachas, 3 leites, café\n\n" +
                           "Sem quantidade, consideramos 1.\n" +
                           "Separe os produtos por vírgula, " +
                           "ponto e vírgula ou quebra de linha.\n" +
                           "Até 15 produtos por mensagem.\n\n" +
                           "/adicionar produtos — adiciona à lista\n" +
                           "/lista — mostra os pendentes\n" +
                           "/comprado código — marca como comprado\n" +
                           "/historico — mostra os comprados\n" +
                           "/desfazer código — devolve aos pendentes\n" +
                           "/remover código — apaga definitivamente\n" +
                           "/ajuda — mostra os comandos\n\n" +
                           "Comprei marca toda a quantidade do item.\n" +
                           "Comprar todos marca todos os pendentes " +
                           "após confirmação.\n\n" +
                           "Histórico: compras de outubro saem em janeiro, " +
                           "novembro em fevereiro e assim por diante.";
                }

            case "/adicionar":
                {
                    if (partes.Length < 2 ||
                        string.IsNullOrWhiteSpace(partes[1]))
                    {
                        return "Informe os produtos. Exemplo:\n" +
                               "/adicionar 2 bolachas, leite, café";
                    }

                    var entradas = partes[1].Split(
                        new[] { ',', ';', '\r', '\n' },
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries);

                    if (entradas.Length == 0)
                    {
                        return "Informe pelo menos um produto.";
                    }

                    if (entradas.Length > 15)
                    {
                        return "Envie até 15 produtos por mensagem. " +
                               "Nenhum item desta mensagem foi adicionado.";
                    }

                    var itens = new List<ItemCompra>();

                    foreach (var entrada in entradas)
                    {
                        if (!TentarLerProduto(
                                entrada,
                                out var nome,
                                out var quantidade,
                                out var erro))
                        {
                            return erro +
                                   "\nNenhum item desta mensagem foi adicionado.";
                        }

                        itens.Add(new ItemCompra
                        {
                            ChatId = chatId,
                            Nome = nome,
                            Quantidade = quantidade,
                            Comprado = false,
                            Mes = agoraBrasilia.Month,
                            Ano = agoraBrasilia.Year,
                            CompradoEm = null
                        });
                    }

                    _context.ItensCompra.AddRange(itens);

                    await _context.SaveChangesAsync(cancellationToken);

                    return $"✅ {itens.Count} item(ns) adicionado(s):\n\n" +
                           string.Join(
                               "\n",
                               itens.Select(item =>
                                   $"• {DescreverItem(item)}"));
                }

            case "/lista":
                {
                    var pendentes = await _context.ItensCompra
                        .AsNoTracking()
                        .Where(item =>
                            item.ChatId == chatId &&
                            !item.Comprado)
                        .OrderBy(item => item.Id)
                        .Take(16)
                        .ToListAsync(cancellationToken);

                    if (pendentes.Count == 0)
                    {
                        return "Não há itens pendentes na sua lista! 🛒";
                    }

                    var linhas = pendentes.Select(item =>
                        $"{item.Id}. {DescreverItem(item)}");

                    return MontarPagina(
                        "🛒 Falta comprar:",
                        linhas);
                }

            case "/comprado":
                {
                    if (!TentarObterId(partes, out var id))
                    {
                        return "Informe o código. Exemplo: /comprado 1";
                    }

                    var item = await BuscarItemAsync(
                        chatId,
                        id,
                        cancellationToken);

                    if (item is null)
                    {
                        return "Item não encontrado na sua lista.";
                    }

                    if (item.Comprado)
                    {
                        return $"{DescreverItem(item)} já está comprado.";
                    }

                    item.Comprado = true;
                    item.CompradoEm = agoraUtc;

                    await _context.SaveChangesAsync(cancellationToken);

                    return $"Comprado: {DescreverItem(item)} ✅";
                }

            case "/historico":
                {
                    var comprados = await _context.ItensCompra
                        .AsNoTracking()
                        .Where(item =>
                            item.ChatId == chatId &&
                            item.Comprado)
                        .OrderByDescending(item => item.CompradoEm)
                        .ThenByDescending(item => item.Id)
                        .Take(16)
                        .ToListAsync(cancellationToken);

                    if (comprados.Count == 0)
                    {
                        return "Seu histórico de compras está vazio.";
                    }

                    var linhas = comprados.Select(item =>
                    {
                        var data = item.CompradoEm.HasValue
                            ? FormatarDataCompra(item.CompradoEm.Value)
                            : "data não registrada";

                        return $"{item.Id}. {DescreverItem(item)} — {data}";
                    });

                    return MontarPagina(
                        "✅ Histórico de compras:",
                        linhas);
                }

            case "/desfazer":
                {
                    if (!TentarObterId(partes, out var id))
                    {
                        return "Informe o código. Exemplo: /desfazer 1";
                    }

                    var item = await BuscarItemAsync(
                        chatId,
                        id,
                        cancellationToken);

                    if (item is null)
                    {
                        return "Item não encontrado na sua lista.";
                    }

                    if (!item.Comprado)
                    {
                        return $"{DescreverItem(item)} já está pendente.";
                    }

                    item.Comprado = false;
                    item.CompradoEm = null;

                    await _context.SaveChangesAsync(cancellationToken);

                    return $"Voltou para os pendentes: {DescreverItem(item)} 🛒";
                }

            case "/remover":
                {
                    if (!TentarObterId(partes, out var id))
                    {
                        return "Informe o código. Exemplo: /remover 1";
                    }

                    var item = await BuscarItemAsync(
                        chatId,
                        id,
                        cancellationToken);

                    if (item is null)
                    {
                        return "Item não encontrado na sua lista.";
                    }

                    _context.ItensCompra.Remove(item);

                    await _context.SaveChangesAsync(cancellationToken);

                    return $"Apagado definitivamente: {DescreverItem(item)} 🗑️";
                }

            default:
                {
                    return "Comando não reconhecido. Use /ajuda.";
                }
        }
    }

    public async Task<int> MarcarTodosCompradosAsync(
        long chatId,
        CancellationToken cancellationToken = default)
    {
        var agoraUtc = DateTime.UtcNow;

        return await _context.ItensCompra
            .Where(item =>
                item.ChatId == chatId &&
                !item.Comprado)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.Comprado, true)
                    .SetProperty(
                        item => item.CompradoEm,
                        (DateTime?)agoraUtc),
                cancellationToken);
    }

    public async Task<TelegramBotao[][]> ObterBotoesListaAsync(
        long chatId,
        CancellationToken cancellationToken = default)
    {
        var itens = await _context.ItensCompra
            .AsNoTracking()
            .Where(item =>
                item.ChatId == chatId &&
                !item.Comprado)
            .OrderBy(item => item.Id)
            .Take(15)
            .ToListAsync(cancellationToken);

        var linhas = new List<TelegramBotao[]>();

        foreach (var item in itens)
        {
            var nomeCurto = item.Nome.Length > 30
                ? item.Nome[..30] + "..."
                : item.Nome;

            linhas.Add(new[]
            {
                new TelegramBotao
                {
                    Texto = $"✅ Comprei {item.Quantidade}x {nomeCurto}",
                    Acao = $"comprado:{item.Id}"
                }
            });
        }

        linhas.Add(new[]
        {
            new TelegramBotao
            {
                Texto = "🔄 Atualizar lista",
                Acao = "lista"
            },
            new TelegramBotao
            {
                Texto = "📜 Histórico",
                Acao = "historico"
            }
        });

        return linhas.ToArray();
    }

    private static bool TentarLerProduto(
        string entrada,
        out string nome,
        out int quantidade,
        out string erro)
    {
        nome = entrada.Trim();
        quantidade = 1;
        erro = string.Empty;

        // Reconhece: "2 bolachas", "2x bolachas" e "2 x bolachas".
        var correspondencia = Regex.Match(
            nome,
            @"^(?<quantidade>[+-]?[0-9]+)(?:\s*[xX]\s*|\s+)(?<nome>.+)$");

        if (correspondencia.Success)
        {
            if (!int.TryParse(
                    correspondencia.Groups["quantidade"].Value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out quantidade) ||
                quantidade < 1 ||
                quantidade > 999)
            {
                erro = "A quantidade deve ser um número inteiro de 1 a 999.";
                return false;
            }

            nome = correspondencia.Groups["nome"].Value.Trim();
        }
        else if (Regex.IsMatch(nome, @"^[+-]?[0-9]+$"))
        {
            erro = "Informe o produto depois da quantidade. Exemplo: 2 bolachas.";
            return false;
        }
        else if (Regex.IsMatch(
                     nome,
                     @"^[+-]?[0-9]+(?:\.[0-9]+|\s*[xX])(?:\s|$)"))
        {
            erro = "Use uma quantidade inteira seguida do produto. " +
                   "Exemplo: 2 bolachas.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            erro = "O nome do produto não pode estar vazio.";
            return false;
        }

        if (nome.Length > 150)
        {
            erro = "Cada nome de produto deve ter até 150 caracteres.";
            return false;
        }

        return true;
    }

    private async Task LimparHistoricoAntigoAsync(
        long chatId,
        DateTime agoraBrasilia,
        CancellationToken cancellationToken)
    {
        var inicioMesAtual = new DateTime(
            agoraBrasilia.Year,
            agoraBrasilia.Month,
            1);

        // Compras de outubro são apagadas a partir de janeiro.
        var limiteLocal = DateTime.SpecifyKind(
            inicioMesAtual.AddMonths(-2),
            DateTimeKind.Unspecified);

        var limiteUtc = TimeZoneInfo.ConvertTimeToUtc(
            limiteLocal,
            FusoBrasilia);

        await _context.ItensCompra
            .Where(item =>
                item.ChatId == chatId &&
                item.Comprado &&
                item.CompradoEm.HasValue &&
                item.CompradoEm.Value < limiteUtc)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private Task<ItemCompra?> BuscarItemAsync(
        long chatId,
        int id,
        CancellationToken cancellationToken)
    {
        return _context.ItensCompra.FirstOrDefaultAsync(
            item => item.Id == id && item.ChatId == chatId,
            cancellationToken);
    }

    private static bool TentarObterId(
        string[] partes,
        out int id)
    {
        id = 0;

        return partes.Length == 2 &&
               int.TryParse(partes[1].Trim(), out id) &&
               id > 0;
    }

    private static string DescreverItem(ItemCompra item)
    {
        return $"{item.Quantidade}x {item.Nome}";
    }

    private static string FormatarDataCompra(DateTime data)
    {
        var dataUtc = DateTime.SpecifyKind(
            data,
            DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(
            dataUtc,
            FusoBrasilia).ToString("dd/MM/yyyy");
    }

    private static string MontarPagina(
        string titulo,
        IEnumerable<string> linhas)
    {
        var mensagem = titulo + "\n\n";
        var exibidos = 0;

        foreach (var linha in linhas)
        {
            if (exibidos >= 15)
            {
                mensagem += "\nExistem mais registros. " +
                            "Esta consulta mostra apenas os primeiros 15.";
                break;
            }

            mensagem += linha + "\n";
            exibidos++;
        }

        return mensagem.TrimEnd();
    }
}