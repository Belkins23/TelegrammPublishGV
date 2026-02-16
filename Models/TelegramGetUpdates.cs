using Newtonsoft.Json;

namespace TelegrammPublishGV.Models;

/// <summary>Ответ Telegram Bot API getUpdates.</summary>
public class TelegramGetUpdatesResponse
{
    [JsonProperty("ok")]
    public bool Ok { get; set; }

    [JsonProperty("result")]
    public List<TelegramUpdate>? Result { get; set; }
}

/// <summary>Обновление (сообщение, нажатие кнопки и т.д.). Приходит в webhook и getUpdates.</summary>
public class TelegramUpdate
{
    [JsonProperty("update_id")]
    public long UpdateId { get; set; }

    [JsonProperty("callback_query")]
    public TelegramCallbackQuery? CallbackQuery { get; set; }
}

/// <summary>Нажатие inline-кнопки.</summary>
public class TelegramCallbackQuery
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("from")]
    public TelegramUser? From { get; set; }

    [JsonProperty("message")]
    public TelegramMessage? Message { get; set; }

    [JsonProperty("data")]
    public string? Data { get; set; }
}

public class TelegramUser
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("first_name")]
    public string? FirstName { get; set; }

    [JsonProperty("username")]
    public string? Username { get; set; }
}

public class TelegramMessage
{
    [JsonProperty("message_id")]
    public int MessageId { get; set; }

    [JsonProperty("chat")]
    public TelegramChat? Chat { get; set; }

    [JsonProperty("text")]
    public string? Text { get; set; }

    [JsonProperty("reply_to_message")]
    public TelegramMessage? ReplyToMessage { get; set; }
}

public class TelegramChat
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("title")]
    public string? Title { get; set; }
}
