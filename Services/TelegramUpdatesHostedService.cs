using Microsoft.Extensions.Options;
using TelegrammPublishGV.Models;

namespace TelegrammPublishGV.Services;

/// <summary>
/// Long polling getUpdates — получает обновления от Telegram. Запускается только когда UseWebhook = false.
/// </summary>
public class TelegramUpdatesHostedService : BackgroundService
{
    private readonly TelegramOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TelegramCallbackProcessor _processor;
    private readonly ILogger<TelegramUpdatesHostedService> _logger;

    public TelegramUpdatesHostedService(
        IOptions<TelegramOptions> options,
        IHttpClientFactory httpClientFactory,
        TelegramCallbackProcessor processor,
        ILogger<TelegramUpdatesHostedService> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _processor = processor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.UseWebhook)
        {
            _logger.LogInformation("UseWebhook=true — getUpdates не запущен");
            return;
        }
        if (string.IsNullOrWhiteSpace(_options.BotToken))
        {
            _logger.LogWarning("Telegram:BotToken не задан — getUpdates не запущен");
            return;
        }

        _logger.LogInformation("Сервис getUpdates (long polling) запущен");
        long offset = 0;
        var baseUrl = $"https://api.telegram.org/bot{_options.BotToken}";

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                var url = $"{baseUrl}/getUpdates?offset={offset}&timeout=30";
                using var response = await client.GetAsync(url, stoppingToken);
                var json = await response.Content.ReadAsStringAsync(stoppingToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("getUpdates ошибка: {StatusCode} — {Response}", response.StatusCode, json);
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                var updates = Newtonsoft.Json.JsonConvert.DeserializeObject<TelegramGetUpdatesResponse>(json);
                if (updates?.Result == null || updates.Result.Count == 0)
                    continue;

                foreach (var update in updates.Result)
                {
                    offset = update.UpdateId + 1;
                    if (update.CallbackQuery != null)
                        await _processor.ProcessCallbackQueryAsync(update.CallbackQuery, stoppingToken);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в цикле getUpdates");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Сервис getUpdates остановлен");
    }
}
