using MercadoBot.Api.DTOs;
using MercadoBot.Api.Services;
using MercadoBot.Api.Context;
using MercadoBot.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;

namespace MercadoBot.Api.Controllers;

[ApiController]
[Route("api/telegram")]
public class TelegramController : ControllerBase
{
    private const string NomeBot = "Pouco_bot";
    private static readonly ConcurrentDictionary<(long, long), string> Pendentes = new();
    private static readonly string[] Dias = ["domingo", "segunda-feira", "terça-feira", "quarta-feira", "quinta-feira", "sexta-feira", "sábado"];
    private readonly AppDbContext _db;

    private readonly TelegramService _telegramService;
    private readonly ListaComprasService _listaComprasService;
    private readonly string _webhookSecret;

    public TelegramController(
        AppDbContext db,
        TelegramService telegramService,
        ListaComprasService listaComprasService,
        IConfiguration configuration)
    {
        _db = db;
        _telegramService = telegramService;
        _listaComprasService = listaComprasService;

        _webhookSecret = configuration["Telegram:WebhookSecret"]
            ?? throw new InvalidOperationException(
                "A chave do webhook não foi configurada.");

        if (string.IsNullOrWhiteSpace(_webhookSecret))
        {
            throw new InvalidOperationException(
                "A chave do webhook não pode estar vazia.");
        }
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> ReceberMensagem(
        [FromBody] TelegramUpdateDto update,
        CancellationToken cancellationToken)
    {
        var chaveRecebida = Request.Headers[
            "X-Telegram-Bot-Api-Secret-Token"].ToString();

        if (!string.Equals(
                chaveRecebida,
                _webhookSecret,
                StringComparison.Ordinal))
        {
            return Unauthorized();
        }

        if (update.CallbackQuery is not null)
        {
            await ProcessarCliqueAsync(
                update.CallbackQuery,
                cancellationToken);

            return Ok();
        }

        var mensagem = update.Message;

        if (mensagem?.Chat is null ||
            string.IsNullOrWhiteSpace(mensagem.Text) ||
            !ChatPermitido(mensagem.Chat))
        {
            return Ok();
        }

        if (mensagem.From is not null && mensagem.ReplyToMessage?.From?.IsBot == true &&
            string.Equals(mensagem.ReplyToMessage.From.Username, NomeBot, StringComparison.OrdinalIgnoreCase) &&
            mensagem.ReplyToMessage.Text?.EndsWith($"Referencia: tarefa:{mensagem.From.Id}", StringComparison.Ordinal) == true)
        {
            var descricao = mensagem.Text.Trim();
            if (descricao.Length is > 0 and <= 300 && !descricao.StartsWith('/'))
            {
                Pendentes[(mensagem.Chat.Id, mensagem.From.Id)] = descricao;
                var botoes = Enumerable.Range(0, 7).Select(d => new[] { new TelegramBotao { Texto = Dias[d], Acao = $"dia_tarefa:{mensagem.From.Id}:{d}" } }).ToArray();
                await _telegramService.EnviarMensagemComBotoesAsync(mensagem.Chat.Id, "Em qual dia da semana?", botoes, cancellationToken);
            }
            else await _telegramService.EnviarMensagemAsync(mensagem.Chat.Id, "Descreva a tarefa em até 300 caracteres, sem comando.", cancellationToken);
            return Ok();
        }

        if (EhRespostaDeInclusao(mensagem))
        {
            await ProcessarProdutoInformadoAsync(
                mensagem,
                cancellationToken);

            return Ok();
        }

        var resposta = await _listaComprasService.ProcessarMensagemAsync(
            mensagem.Chat.Id,
            mensagem.Text,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(resposta))
        {
            return Ok();
        }

        var comando = mensagem.Text.Trim()
            .Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0]
            .Split('@', 2)[0];

        await EnviarRespostaAsync(
            mensagem.Chat.Id,
            resposta,
            mostrarItens: comando.Equals(
                "/lista",
                StringComparison.OrdinalIgnoreCase),
            cancellationToken: cancellationToken);

        return Ok();
    }

    private async Task ProcessarCliqueAsync(
        TelegramCallbackQueryDto clique,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clique.Id))
        {
            return;
        }

        await _telegramService.ConfirmarCliqueAsync(
            clique.Id,
            cancellationToken);

        var chat = clique.Message?.Chat;

        if (chat is null ||
            !ChatPermitido(chat) ||
            string.IsNullOrWhiteSpace(clique.Data))
        {
            return;
        }

        if (clique.Data == "tarefas")
        {
            var tarefas = await _db.TarefasCasa.AsNoTracking().Where(t => t.ChatId == chat.Id).OrderBy(t => t.Descricao).ToListAsync(cancellationToken);
            var texto = tarefas.Count == 0 ? "Nenhuma tarefa cadastrada ainda." :
                string.Join("\n\n", Enumerable.Range(0, 7).Select(i => (i + 1) % 7).Select(d =>
                    $"{char.ToUpperInvariant(Dias[d][0])}{Dias[d][1..]}\n" + string.Join("\n", tarefas.Where(t => (int)t.DiaSemana == d).Select(t => $"• {t.Descricao}"))));
            await _telegramService.EnviarMensagemComBotoesAsync(chat.Id, texto,
                [[new TelegramBotao { Texto = "➕ Adicionar tarefa", Acao = "adicionar_tarefa" }]], cancellationToken);
            return;
        }
        if (clique.Data == "adicionar_tarefa" && clique.From is not null)
        {
            Pendentes.TryRemove((chat.Id, clique.From.Id), out _);
            await _telegramService.SolicitarTarefaAsync(chat.Id, clique.Message!.MessageId, clique.From.Id, cancellationToken);
            return;
        }
        var partesDia = clique.Data.Split(':');
        if (partesDia.Length == 3 && partesDia[0] == "dia_tarefa" && clique.From is not null &&
            long.TryParse(partesDia[1], out var solicitante) && solicitante == clique.From.Id &&
            int.TryParse(partesDia[2], out var dia) && dia is >= 0 and <= 6)
        {
            if (Pendentes.TryRemove((chat.Id, clique.From.Id), out var descricao))
            {
                _db.TarefasCasa.Add(new TarefaCasa { ChatId = chat.Id, Descricao = descricao, DiaSemana = (DayOfWeek)dia });
                await _db.SaveChangesAsync(cancellationToken);
                await _telegramService.RemoverBotoesAsync(chat.Id, clique.Message!.MessageId, cancellationToken);
                await _telegramService.EnviarMensagemAsync(chat.Id, $"Tarefa salva: {descricao} — {Dias[dia]}.", cancellationToken);
            }
            return;
        }
        if (clique.Data == "adicionar")
        {
            if (clique.From is null)
            {
                return;
            }

            await _telegramService.SolicitarProdutoAsync(
                chat.Id,
                clique.Message!.MessageId,
                clique.From.Id,
                cancellationToken);

            return;
        }

        if (clique.Data == "comprar_todos")
        {
            if (clique.From is null)
            {
                return;
            }

            await SolicitarConfirmacaoTodosAsync(
                chat.Id,
                clique.From.Id,
                cancellationToken);

            return;
        }

        if (clique.Data.StartsWith(
                "confirmar_todos:",
                StringComparison.Ordinal))
        {
            if (!CliqueDoSolicitante(clique, "confirmar_todos:"))
            {
                await _telegramService.EnviarMensagemAsync(
                    chat.Id,
                    "Somente quem pediu pode confirmar. " +
                    "Use o botão Comprar todos para iniciar sua confirmação.",
                    cancellationToken);

                return;
            }

            await _telegramService.RemoverBotoesAsync(
                chat.Id,
                clique.Message!.MessageId,
                cancellationToken);

            var quantidade =
                await _listaComprasService.MarcarTodosCompradosAsync(
                    chat.Id,
                    cancellationToken);

            var texto = quantidade == 0
                ? "Não havia itens pendentes."
                : $"✅ {quantidade} item(ns) marcado(s) como comprado(s).";

            await _telegramService.EnviarMensagemAsync(
                chat.Id,
                texto,
                cancellationToken);

            await EnviarListaAsync(chat.Id, cancellationToken);

            return;
        }

        if (clique.Data.StartsWith(
                "cancelar_todos:",
                StringComparison.Ordinal))
        {
            if (!CliqueDoSolicitante(clique, "cancelar_todos:"))
            {
                return;
            }

            await _telegramService.RemoverBotoesAsync(
                chat.Id,
                clique.Message!.MessageId,
                cancellationToken);

            await EnviarRespostaAsync(
                chat.Id,
                "Operação cancelada. Nenhum item foi alterado.",
                mostrarItens: false,
                cancellationToken: cancellationToken);

            return;
        }

        string comando;

        if (clique.Data == "lista")
        {
            comando = "/lista";
        }
        else if (clique.Data == "historico")
        {
            comando = "/historico";
        }
        else if (clique.Data.StartsWith(
                     "comprado:",
                     StringComparison.Ordinal) &&
                 int.TryParse(
                     clique.Data["comprado:".Length..],
                     out var id) &&
                 id > 0)
        {
            comando = $"/comprado {id}";
        }
        else
        {
            return;
        }

        var resposta = await _listaComprasService.ProcessarMensagemAsync(
            chat.Id,
            comando,
            cancellationToken);

        if (comando.StartsWith("/comprado ", StringComparison.Ordinal))
        {
            await _telegramService.EnviarMensagemAsync(
                chat.Id,
                resposta,
                cancellationToken);

            await EnviarListaAsync(chat.Id, cancellationToken);

            return;
        }

        await EnviarRespostaAsync(
            chat.Id,
            resposta,
            mostrarItens: comando == "/lista",
            cancellationToken: cancellationToken);
    }

    private async Task SolicitarConfirmacaoTodosAsync(
        long chatId,
        long usuarioId,
        CancellationToken cancellationToken)
    {
        var botoes = new[]
        {
            new[]
            {
                new TelegramBotao
                {
                    Texto = "✅ Confirmar",
                    Acao = $"confirmar_todos:{usuarioId}"
                },
                new TelegramBotao
                {
                    Texto = "❌ Cancelar",
                    Acao = $"cancelar_todos:{usuarioId}"
                }
            }
        };

        await _telegramService.EnviarMensagemComBotoesAsync(
            chatId,
            "Marcar TODOS os itens pendentes desta lista como comprados?\n\n" +
            "Isso inclui os itens que não aparecem na primeira página.\n" +
            "Serão considerados os pendentes no momento da confirmação.",
            botoes,
            cancellationToken);
    }

    private static bool CliqueDoSolicitante(
        TelegramCallbackQueryDto clique,
        string prefixo)
    {
        return clique.From is not null &&
               long.TryParse(
                   clique.Data![prefixo.Length..],
                   out var usuarioId) &&
               usuarioId == clique.From.Id;
    }

    private static bool EhRespostaDeInclusao(
        TelegramMessageDto mensagem)
    {
        var pergunta = mensagem.ReplyToMessage;

        if (mensagem.From is null ||
            pergunta?.From is null ||
            !pergunta.From.IsBot ||
            !string.Equals(
                pergunta.From.Username,
                NomeBot,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var referencia = $"Referencia: adicionar:{mensagem.From.Id}";

        return pergunta.Text?.EndsWith(
            referencia,
            StringComparison.Ordinal) == true;
    }

    private async Task ProcessarProdutoInformadoAsync(
        TelegramMessageDto mensagem,
        CancellationToken cancellationToken)
    {
        var chatId = mensagem.Chat!.Id;
        var texto = mensagem.Text!.Trim();

        var primeiroTermo = texto
            .Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0]
            .Split('@', 2)[0];

        if (primeiroTermo.Equals(
                "/cancelar",
                StringComparison.OrdinalIgnoreCase))
        {
            await EnviarRespostaAsync(
                chatId,
                "Inclusão cancelada.",
                mostrarItens: false,
                cancellationToken: cancellationToken);

            return;
        }

        if (texto.StartsWith('/'))
        {
            await EnviarRespostaAsync(
                chatId,
                "Responda à pergunta com o nome do produto, " +
                "sem comando. Para cancelar, responda /cancelar.",
                mostrarItens: false,
                cancellationToken: cancellationToken);

            return;
        }

        var resposta = await _listaComprasService.ProcessarMensagemAsync(
            chatId,
            $"/adicionar {texto}",
            cancellationToken);

        await EnviarRespostaAsync(
            chatId,
            resposta,
            mostrarItens: false,
            cancellationToken: cancellationToken);
    }

    private async Task EnviarListaAsync(
        long chatId,
        CancellationToken cancellationToken)
    {
        var resposta = await _listaComprasService.ProcessarMensagemAsync(
            chatId,
            "/lista",
            cancellationToken);

        await EnviarRespostaAsync(
            chatId,
            resposta,
            mostrarItens: true,
            cancellationToken: cancellationToken);
    }

    private async Task EnviarRespostaAsync(
        long chatId,
        string texto,
        bool mostrarItens,
        CancellationToken cancellationToken)
    {
        TelegramBotao[][] botoes;

        if (mostrarItens)
        {
            var botoesLista =
                await _listaComprasService.ObterBotoesListaAsync(
                    chatId,
                    cancellationToken);

            var linhas = botoesLista.ToList();

            linhas.Insert(0, new[]
            {
                new TelegramBotao
                {
                    Texto = "➕ Adicionar item",
                    Acao = "adicionar"
                }
            });

            linhas.Add(new[]
            {
                new TelegramBotao
                {
                    Texto = "✅ Comprar todos",
                    Acao = "comprar_todos"
                }
            });

            botoes = linhas.ToArray();
        }
        else
        {
            botoes = new[]
            {
                new[]
                {
                    new TelegramBotao
                    {
                        Texto = "➕ Adicionar item",
                        Acao = "adicionar"
                    }
                },
                new[]
                {
                    new TelegramBotao
                    {
                        Texto = "🛒 O que falta comprar",
                        Acao = "lista"
                    },
                    new TelegramBotao
                    {
                        Texto = "📜 Histórico",
                        Acao = "historico"
                    },
                    new TelegramBotao
                    {
                        Texto = "🏠 Tarefas de casa",
                        Acao = "tarefas"
                    }
                }
            };
        }

        await _telegramService.EnviarMensagemComBotoesAsync(
            chatId,
            texto,
            botoes,
            cancellationToken);
    }

    private static bool ChatPermitido(TelegramChatDto chat)
    {
        return chat.Type is "private" or "group" or "supergroup";
    }
}
