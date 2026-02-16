using Microsoft.AspNetCore.Mvc;
using TelegrammPublishGV.Models;
using TelegrammPublishGV.Services;

namespace TelegrammPublishGV.Controllers;

[ApiController]
[Route("api/telegram")]
public class TelegramWebhookController : ControllerBase
{
    private readonly TelegramCallbackProcessor _processor;
    private readonly ILogger<TelegramWebhookController> _logger;

    public TelegramWebhookController(TelegramCallbackProcessor processor, ILogger<TelegramWebhookController> logger)
    {
        _processor = processor;
        _logger = logger;
    }

    /// <summary>
    /// Webhook для приёма обновлений от Telegram. URL задаётся в setWebhook (например https://твой-домен.ru/api/telegram/webhook).
    /// </summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] TelegramUpdate update, CancellationToken cancellationToken)
    {
        if (update?.CallbackQuery != null)
        {
            try
            {
                await _processor.ProcessCallbackQueryAsync(update.CallbackQuery, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка обработки callback_query");
                // Всё равно возвращаем 200, чтобы Telegram не повторял доставку
            }
        }
        return Ok();
    }
}
