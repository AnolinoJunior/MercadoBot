using System.Text.Json.Serialization;

namespace MercadoBot.Api.DTOs;

public class TelegramUpdateDto
{
    [JsonPropertyName("update_id")]
    public long UpdateId { get; set; }

    [JsonPropertyName("message")]
    public TelegramMessageDto? Message { get; set; }

    [JsonPropertyName("callback_query")]
    public TelegramCallbackQueryDto? CallbackQuery { get; set; }
}

public class TelegramMessageDto
{
    [JsonPropertyName("message_id")]
    public int MessageId { get; set; }

    [JsonPropertyName("from")]
    public TelegramUserDto? From { get; set; }

    [JsonPropertyName("chat")]
    public TelegramChatDto? Chat { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("reply_to_message")]
    public TelegramMessageDto? ReplyToMessage { get; set; }
}

public class TelegramChatDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public class TelegramUserDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("is_bot")]
    public bool IsBot { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }
}

public class TelegramCallbackQueryDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("from")]
    public TelegramUserDto? From { get; set; }

    [JsonPropertyName("message")]
    public TelegramMessageDto? Message { get; set; }

    [JsonPropertyName("data")]
    public string? Data { get; set; }
}