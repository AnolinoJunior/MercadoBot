using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MercadoBot.Api.Services;

public class TelegramService
{
    private readonly HttpClient _httpClient;
    private readonly string _botToken;

    public TelegramService(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;

        _botToken = configuration["Telegram:BotToken"]
            ?? throw new InvalidOperationException(
                "O token do Telegram não foi configurado.");

        if (string.IsNullOrWhiteSpace(_botToken))
        {
            throw new InvalidOperationException(
                "O token do Telegram não pode estar vazio.");
        }
    }

    public Task EnviarMensagemAsync(
        long chatId,
        string texto,
        CancellationToken cancellationToken = default)
    {
        return ExecutarAsync(
            "sendMessage",
            new
            {
                chat_id = chatId,
                text = texto
            },
            cancellationToken);
    }

    public Task EnviarMensagemComBotoesAsync(
        long chatId,
        string texto,
        TelegramBotao[][] botoes,
        CancellationToken cancellationToken = default)
    {
        return ExecutarAsync(
            "sendMessage",
            new
            {
                chat_id = chatId,
                text = texto,
                reply_markup = new
                {
                    inline_keyboard = botoes
                }
            },
            cancellationToken);
    }

    public Task SolicitarProdutoAsync(
        long chatId,
        int mensagemOrigemId,
        long usuarioId,
        CancellationToken cancellationToken = default)
    {
        return ExecutarAsync(
            "sendMessage",
            new
            {
                chat_id = chatId,

                text =
                    $"<a href=\"tg://user?id={usuarioId}\">Adicionar produtos</a>\n" +
                    "Quais produtos deseja adicionar?\n" +
                    "Responda a esta mensagem separando os produtos " +
                    "por vírgula, ponto e vírgula ou um por linha.\n" +
                    "Envie até 15 produtos por mensagem.\n" +
                    "Para cancelar, responda /cancelar.\n" +
                    $"Referencia: adicionar:{usuarioId}",

                parse_mode = "HTML",

                reply_parameters = new
                {
                    message_id = mensagemOrigemId,
                    allow_sending_without_reply = true
                },

                reply_markup = new
                {
                    force_reply = true,
                    selective = true,
                    input_field_placeholder = "Digite seus produtos"
                }
            },
            cancellationToken);
    }

    public Task ConfirmarCliqueAsync(
        string callbackQueryId,
        CancellationToken cancellationToken = default)
    {
        return ExecutarAsync(
            "answerCallbackQuery",
            new
            {
                callback_query_id = callbackQueryId
            },
            cancellationToken);
    }

    public Task RemoverBotoesAsync(
        long chatId,
        int mensagemId,
        CancellationToken cancellationToken = default)
    {
        return ExecutarAsync(
            "editMessageReplyMarkup",
            new
            {
                chat_id = chatId,
                message_id = mensagemId,
                reply_markup = new
                {
                    inline_keyboard = Array.Empty<TelegramBotao[]>()
                }
            },
            cancellationToken);
    }

    private async Task ExecutarAsync(
        string metodo,
        object dados,
        CancellationToken cancellationToken)
    {
        using var resposta = await _httpClient.PostAsJsonAsync(
            $"https://api.telegram.org/bot{_botToken}/{metodo}",
            dados,
            cancellationToken);

        if (!resposta.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Falha na operação {metodo} do Telegram. " +
                $"Status HTTP: {(int)resposta.StatusCode}.");
        }
    }
}

public class TelegramBotao
{
    [JsonPropertyName("text")]
    public string Texto { get; set; } = string.Empty;

    [JsonPropertyName("callback_data")]
    public string Acao { get; set; } = string.Empty;
}