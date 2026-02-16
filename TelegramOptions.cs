namespace TelegrammPublishGV;

public class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>Использовать webhook вместо getUpdates. Нужен HTTPS и публичный URL (WebhookBaseUrl).</summary>
    public bool UseWebhook { get; set; }

    /// <summary>Базовый URL приложения (https://твой-домен.ru) — для регистрации webhook в Telegram.</summary>
    public string WebhookBaseUrl { get; set; } = string.Empty;
}
