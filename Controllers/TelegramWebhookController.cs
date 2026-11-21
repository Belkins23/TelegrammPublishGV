using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using TelegrammPublishGV;
using TelegrammPublishGV.Models;
using TelegrammPublishGV.Services;

namespace TelegrammPublishGV.Controllers;

[ApiController]
[Route("api/telegram")]
public class TelegramWebhookController : ControllerBase
{
    private readonly TelegramCallbackProcessor _processor;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramWebhookController> _logger;

    public TelegramWebhookController(TelegramCallbackProcessor processor, IOptions<TelegramOptions> options, ILogger<TelegramWebhookController> logger)
    {
        _processor = processor;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Webhook для приёма обновлений от Telegram. URL задаётся в setWebhook (например https://твой-домен.ru/api/telegram/webhook).
    /// </summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] TelegramUpdate update, CancellationToken cancellationToken)
    {
        if (!_options.UseWebhook)
            return NotFound();

        var supplied = Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString();
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret) || !SecretEquals(supplied, _options.WebhookSecret))
            return Unauthorized();

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

    private static bool SecretEquals(string supplied, string expected)
    {
        var left = Encoding.UTF8.GetBytes(supplied);
        var right = Encoding.UTF8.GetBytes(expected);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}
