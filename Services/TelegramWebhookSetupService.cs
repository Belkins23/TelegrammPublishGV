using Microsoft.Extensions.Options;

namespace TelegrammPublishGV.Services;

/// <summary>
/// При старте приложения регистрирует или снимает webhook в Telegram в зависимости от настроек.
/// </summary>
public class TelegramWebhookSetupService : IHostedService
{
    private readonly TelegramOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TelegramWebhookSetupService> _logger;

    public TelegramWebhookSetupService(
        IOptions<TelegramOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<TelegramWebhookSetupService> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BotToken))
            return;

        var baseUrl = $"https://api.telegram.org/bot{_options.BotToken}";

        if (_options.UseWebhook && !string.IsNullOrWhiteSpace(_options.WebhookBaseUrl))
        {
            var webhookUrl = _options.WebhookBaseUrl.TrimEnd('/') + "/api/telegram/webhook";
            try
            {
                using var client = _httpClientFactory.CreateClient();
                var url = $"{baseUrl}/setWebhook?url={Uri.EscapeDataString(webhookUrl)}";
                using var response = await client.GetAsync(url, cancellationToken);
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                if (response.IsSuccessStatusCode)
                    _logger.LogInformation("Webhook зарегистрирован: {Url}", webhookUrl);
                else
                    _logger.LogWarning("setWebhook ошибка: {Response}", json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не удалось зарегистрировать webhook");
            }
        }
        else
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                var url = $"{baseUrl}/deleteWebhook";
                using var response = await client.GetAsync(url, cancellationToken);
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                if (response.IsSuccessStatusCode)
                    _logger.LogInformation("Webhook снят (будет использоваться getUpdates)");
                else
                    _logger.LogWarning("deleteWebhook ошибка: {Response}", json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не удалось снять webhook");
            }
        }

        await Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
