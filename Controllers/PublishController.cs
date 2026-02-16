using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TelegrammPublishGV.Models;
using TelegrammPublishGV;
using TelegrammPublishGV.Services;

namespace TelegrammPublishGV.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PublishController : ControllerBase
{
    private readonly TelegramOptions _telegramOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOrderPayloadStore _orderPayloadStore;
    private readonly IOrderStatePublisher _orderStatePublisher;
    private readonly ILogger<PublishController> _logger;

    public PublishController(
        IOptions<TelegramOptions> telegramOptions,
        IHttpClientFactory httpClientFactory,
        IOrderPayloadStore orderPayloadStore,
        IOrderStatePublisher orderStatePublisher,
        ILogger<PublishController> logger)
    {
        _telegramOptions = telegramOptions.Value;
        _httpClientFactory = httpClientFactory;
        _orderPayloadStore = orderPayloadStore;
        _orderStatePublisher = orderStatePublisher;
        _logger = logger;
    }

    /// <summary>
    /// Публикует сообщение в Telegram-канал (настройки канала в appsettings).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PublishResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Publish([FromBody] PublishRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_telegramOptions.BotToken) || string.IsNullOrWhiteSpace(_telegramOptions.ChannelId))
        {
            _logger.LogWarning("Telegram BotToken или ChannelId не заданы в appsettings");
            return BadRequest("Telegram BotToken или ChannelId не заданы в appsettings.");
        }

        if (string.IsNullOrWhiteSpace(request?.Text))
        {
            _logger.LogWarning("Запрос на публикацию с пустым текстом");
            return BadRequest("Текст сообщения не может быть пустым.");
        }

        var chatId = !string.IsNullOrWhiteSpace(request.ChatId) ? request.ChatId : _telegramOptions.ChannelId;

        var url = $"https://api.telegram.org/bot{_telegramOptions.BotToken}/sendMessage";
        var body = new TelegramSendMessageRequest
        {
            ChatId = chatId,
            Text = request.Text,
            ParseMode = request.ParseMode,
            ReplyMarkup = request.ReplyMarkup
        };

        _logger.LogInformation("Отправка сообщения в Telegram-канал {ChannelId}", chatId);

        using var client = _httpClientFactory.CreateClient();
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(body, new Newtonsoft.Json.JsonSerializerSettings { NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore });
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(url, content, cancellationToken);

        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Ошибка Telegram API: {StatusCode}, {Response}", response.StatusCode, responseText);
            return Problem(
                detail: responseText,
                statusCode: (int)response.StatusCode,
                title: "Ошибка при отправке в Telegram");
        }

        var result = Newtonsoft.Json.JsonConvert.DeserializeObject<TelegramSendMessageResponse>(responseText);
        if (result?.Ok != true)
        {
            _logger.LogError("Telegram API вернул ok=false. Ответ: {Response}", responseText);
            return Problem("Telegram API вернул ошибку.", statusCode: 502);
        }

        var orderId = OrderPayloadHelper.ExtractOrderId(request);
        if (!string.IsNullOrEmpty(orderId))
        {
            try
            {
                await _orderPayloadStore.SaveAsync(orderId, request, cancellationToken);
                if (result.Result != null)
                    await _orderPayloadStore.SaveMessageLocationAsync(orderId, chatId, result.Result.MessageId, cancellationToken);
                _logger.LogInformation("Payload и расположение заказа {OrderId} сохранены в Redis", orderId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Не удалось сохранить данные заказа {OrderId} в Redis", orderId);
            }
        }

        if (!string.IsNullOrEmpty(orderId) && result.Result != null)
        {
            await _orderStatePublisher.PublishAsync(new OrderDeliveryStateMessage
            {
                OrderId = orderId,
                Status = OrderDeliveryStatus.Published,
                Timestamp = DateTime.UtcNow,
                MessageId = result.Result.MessageId,
                ChatId = chatId
            }, cancellationToken);
        }

        _logger.LogInformation("Сообщение опубликовано в канал, message_id={MessageId}", result.Result?.MessageId);
        return Ok(new PublishResponse { Success = true, MessageId = result.Result?.MessageId });
    }

    /// <summary>
    /// Удаляет публикацию заказа из канала по id заказа. Работает только для заказов, опубликованных после сохранения расположения в Redis.
    /// </summary>
    [HttpDelete("order/{orderId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> DeleteByOrderId(string orderId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            return BadRequest("orderId не указан.");

        if (string.IsNullOrWhiteSpace(_telegramOptions.BotToken))
        {
            _logger.LogWarning("Telegram:BotToken не задан");
            return BadRequest("Telegram BotToken не задан в appsettings.");
        }

        var location = await _orderPayloadStore.GetMessageLocationAsync(orderId, cancellationToken);
        if (location == null)
        {
            _logger.LogInformation("Публикация заказа {OrderId} не найдена в Redis", orderId);
            return NotFound("Публикация заказа не найдена (нет в Redis или устарела).");
        }

        var url = $"https://api.telegram.org/bot{_telegramOptions.BotToken}/deleteMessage";
        var body = new { chat_id = location.ChatId, message_id = location.MessageId };
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(body);
        using var client = _httpClientFactory.CreateClient();
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(url, content, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Ошибка Telegram deleteMessage: {StatusCode}, {Response}", response.StatusCode, responseText);
            return Problem(detail: responseText, statusCode: (int)response.StatusCode, title: "Ошибка при удалении сообщения в Telegram");
        }

        try
        {
            await _orderPayloadStore.DeleteAsync(orderId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось удалить данные заказа {OrderId} из Redis", orderId);
        }

        await _orderStatePublisher.PublishAsync(new OrderDeliveryStateMessage
        {
            OrderId = orderId,
            Status = OrderDeliveryStatus.Deleted,
            Timestamp = DateTime.UtcNow
        }, cancellationToken);

        _logger.LogInformation("Публикация заказа {OrderId} удалена из канала", orderId);
        return NoContent();
    }
}
