using Newtonsoft.Json;

namespace TelegrammPublishGV.Models;

/// <summary>Тело запроса к Telegram Bot API sendMessage (для сериализации).</summary>
internal class TelegramSendMessageRequest
{
    [JsonProperty("chat_id")]
    public string? ChatId { get; set; }

    [JsonProperty("text")]
    public string Text { get; set; } = string.Empty;

    [JsonProperty("parse_mode", NullValueHandling = NullValueHandling.Ignore)]
    public string? ParseMode { get; set; }

    [JsonProperty("reply_markup", NullValueHandling = NullValueHandling.Ignore)]
    public ReplyMarkup? ReplyMarkup { get; set; }
}

public class PublishRequest
{
    /// <summary>ID чата/канала. Если не указан — берётся из appsettings (Telegram:ChannelId).</summary>
    [JsonProperty("chat_id", NullValueHandling = NullValueHandling.Ignore)]
    public string? ChatId { get; set; }

    /// <summary>Текст сообщения. Поддерживает HTML при parse_mode = "HTML".</summary>
    [JsonProperty("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>Режим разбора: "HTML" или "Markdown".</summary>
    [JsonProperty("parse_mode", NullValueHandling = NullValueHandling.Ignore)]
    public string? ParseMode { get; set; }

    /// <summary>Клавиатура (кнопки под сообщением).</summary>
    [JsonProperty("reply_markup", NullValueHandling = NullValueHandling.Ignore)]
    public ReplyMarkup? ReplyMarkup { get; set; }
}

public class ReplyMarkup
{
    [JsonProperty("inline_keyboard")]
    public List<List<InlineKeyboardButton>>? InlineKeyboard { get; set; }
}

public class InlineKeyboardButton
{
    [JsonProperty("text")]
    public string Text { get; set; } = string.Empty;

    [JsonProperty("callback_data", NullValueHandling = NullValueHandling.Ignore)]
    public string? CallbackData { get; set; }

    [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
    public string? Url { get; set; }
}
