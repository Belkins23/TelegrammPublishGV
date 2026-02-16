using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using TelegrammPublishGV.Models;

namespace TelegrammPublishGV.Services;

/// <summary>
/// Обработка callback_query (нажатия кнопок). Используется и getUpdates, и webhook.
/// </summary>
public class TelegramCallbackProcessor
{
    private readonly TelegramOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOrderPayloadStore _orderPayloadStore;
    private readonly IOrderStatePublisher _orderStatePublisher;
    private readonly ILogger<TelegramCallbackProcessor> _logger;

    private readonly ConcurrentDictionary<(long ChatId, int MessageId), byte> _processedTakeOrderMessages = new();
    private readonly ConcurrentDictionary<(long ChatId, int MessageId), long> _takeReplyMessageOwner = new();

    public TelegramCallbackProcessor(
        IOptions<TelegramOptions> options,
        IHttpClientFactory httpClientFactory,
        IOrderPayloadStore orderPayloadStore,
        IOrderStatePublisher orderStatePublisher,
        ILogger<TelegramCallbackProcessor> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _orderPayloadStore = orderPayloadStore;
        _orderStatePublisher = orderStatePublisher;
        _logger = logger;
    }

    public async Task ProcessCallbackQueryAsync(TelegramCallbackQuery callback, CancellationToken ct = default)
    {
        var baseUrl = $"https://api.telegram.org/bot{_options.BotToken}";
        var callbackId = callback.Id;
        var data = callback.Data ?? "";
        var from = callback.From;
        var userName = from != null
            ? (string.IsNullOrEmpty(from.Username) ? (from.FirstName ?? "Пользователь") : $"{from.FirstName} (@{from.Username})".Trim())
            : "Пользователь";

        _logger.LogInformation("Нажата кнопка: callback_data={Data}, от пользователя {User}", data, userName);

        var msg = callback.Message;
        var isTakeOrder = data.StartsWith("take_", StringComparison.OrdinalIgnoreCase);
        var isDelivered = data.StartsWith("delivered_", StringComparison.OrdinalIgnoreCase);
        var isRefuse = data.StartsWith("refuse_", StringComparison.OrdinalIgnoreCase);

        if (isDelivered && msg != null && msg.Chat != null && from != null)
        {
            var deliveredOrderId = data.Length > 10 ? data[10..] : "?";
            await HandleDeliveredAsync(baseUrl, callback, msg, from.Id, userName, deliveredOrderId, ct);
            return;
        }

        if (isRefuse && msg != null && msg.Chat != null && from != null)
        {
            await HandleRefuseAsync(baseUrl, callback, msg, from.Id, userName, data, ct);
            return;
        }

        if (isTakeOrder && msg != null && msg.Chat != null)
        {
            var key = (msg.Chat.Id, msg.MessageId);
            if (!_processedTakeOrderMessages.TryAdd(key, 0))
            {
                await AnswerCallbackQueryAsync(baseUrl, callbackId, "Заказ уже взят", showAlert: true, ct);
                _logger.LogInformation("Повторное нажатие «Забрать заказ» для сообщения {ChatId}/{MessageId}, пользователь {User}", key.Item1, key.Item2, userName);
                return;
            }
        }

        await AnswerCallbackQueryAsync(baseUrl, callbackId, "Принято", showAlert: false, ct);

        if (msg != null && msg.Chat != null)
        {
            try
            {
                using var editClient = _httpClientFactory.CreateClient();
                var editUrl = $"{baseUrl}/editMessageReplyMarkup";
                var editBody = new { chat_id = msg.Chat.Id, message_id = msg.MessageId, reply_markup = new { inline_keyboard = Array.Empty<object>() } };
                var editJson = Newtonsoft.Json.JsonConvert.SerializeObject(editBody);
                using var editContent = new StringContent(editJson, System.Text.Encoding.UTF8, "application/json");
                using var editResponse = await editClient.PostAsync(editUrl, editContent, ct);
                if (!editResponse.IsSuccessStatusCode)
                {
                    var err = await editResponse.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning("editMessageReplyMarkup ошибка: {StatusCode} — {Response}", editResponse.StatusCode, err);
                }
                else if (isTakeOrder && from != null)
                {
                    var orderId = data.Length > 5 ? data[5..] : "?";
                    var sendUrl = $"{baseUrl}/sendMessage";
                    var sendBody = new
                    {
                        chat_id = msg.Chat.Id,
                        text = "👤 Заказ заберёт: " + userName,
                        reply_to_message_id = msg.MessageId,
                        reply_markup = new
                        {
                            inline_keyboard = new[] { new[] { new { text = "✅ Доставил", callback_data = "delivered_" + orderId }, new { text = "Отказаться", callback_data = "refuse_" + orderId } } }
                        }
                    };
                    var sendJson = Newtonsoft.Json.JsonConvert.SerializeObject(sendBody);
                    using var sendContent = new StringContent(sendJson, System.Text.Encoding.UTF8, "application/json");
                    using var sendResponse = await editClient.PostAsync(sendUrl, sendContent, ct);
                    var sendResponseText = await sendResponse.Content.ReadAsStringAsync(ct);
                    if (sendResponse.IsSuccessStatusCode)
                    {
                        var sendResult = Newtonsoft.Json.JsonConvert.DeserializeObject<TelegramSendMessageResponse>(sendResponseText);
                        if (sendResult?.Result != null)
                            _takeReplyMessageOwner.TryAdd((msg.Chat.Id, sendResult.Result.MessageId), from.Id);
                    }
                    _logger.LogInformation("Заказ взят: orderId={OrderId}, пользователь={User}", orderId, userName);
                    await _orderStatePublisher.PublishAsync(new OrderDeliveryStateMessage
                    {
                        OrderId = orderId,
                        Status = OrderDeliveryStatus.Taken,
                        Timestamp = DateTime.UtcNow,
                        UserId = from.Id,
                        UserName = userName
                    }, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при редактировании/отправке сообщения");
            }
        }
    }

    private async Task HandleDeliveredAsync(string baseUrl, TelegramCallbackQuery callback, TelegramMessage msg, long fromUserId, string userName, string orderId, CancellationToken ct)
    {
        var key = (msg.Chat!.Id, msg.MessageId);
        if (!_takeReplyMessageOwner.TryGetValue(key, out var allowedUserId) || allowedUserId != fromUserId)
        {
            await AnswerCallbackQueryAsync(baseUrl, callback.Id, "Только тот, кто забрал заказ, может отметить доставку.", showAlert: true, ct);
            _logger.LogInformation("«Доставил» нажал не тот пользователь: {User}", userName);
            return;
        }
        await AnswerCallbackQueryAsync(baseUrl, callback.Id, "Принято", showAlert: false, ct);
        try
        {
            using var client = _httpClientFactory.CreateClient();
            var editUrl = $"{baseUrl}/editMessageText";
            var newText = (msg.Text ?? "👤 Заказ заберёт: " + userName) + "\n✅ Доставлен";
            var editBody = new { chat_id = msg.Chat.Id, message_id = msg.MessageId, text = newText, reply_markup = new { inline_keyboard = Array.Empty<object>() } };
            var editJson = Newtonsoft.Json.JsonConvert.SerializeObject(editBody);
            using var editContent = new StringContent(editJson, System.Text.Encoding.UTF8, "application/json");
            await client.PostAsync(editUrl, editContent, ct);
            _takeReplyMessageOwner.TryRemove(key, out _);
            _logger.LogInformation("Заказ доставлен, пользователь={User}", userName);
            await _orderStatePublisher.PublishAsync(new OrderDeliveryStateMessage
            {
                OrderId = orderId,
                Status = OrderDeliveryStatus.Delivered,
                Timestamp = DateTime.UtcNow,
                UserId = fromUserId,
                UserName = userName
            }, ct);
        }
        catch (Exception ex) { _logger.LogError(ex, "Ошибка при отметке доставки"); }
    }

    private async Task HandleRefuseAsync(string baseUrl, TelegramCallbackQuery callback, TelegramMessage msg, long fromUserId, string userName, string data, CancellationToken ct)
    {
        var key = (msg.Chat!.Id, msg.MessageId);
        if (!_takeReplyMessageOwner.TryGetValue(key, out var allowedUserId) || allowedUserId != fromUserId)
        {
            await AnswerCallbackQueryAsync(baseUrl, callback.Id, "Только тот, кто забрал заказ, может отказаться.", showAlert: true, ct);
            return;
        }
        await AnswerCallbackQueryAsync(baseUrl, callback.Id, "Заказ снят с вас", showAlert: false, ct);
        var orderMessage = msg.ReplyToMessage;
        var orderId = data.Length > 7 ? data[7..] : "?";
        try
        {
            using var client = _httpClientFactory.CreateClient();
            var deleteUrl = $"{baseUrl}/deleteMessage";
            var deleteBody = new { chat_id = msg.Chat.Id, message_id = msg.MessageId };
            var deleteJson = Newtonsoft.Json.JsonConvert.SerializeObject(deleteBody);
            using var deleteContent = new StringContent(deleteJson, System.Text.Encoding.UTF8, "application/json");
            await client.PostAsync(deleteUrl, deleteContent, ct);
            _takeReplyMessageOwner.TryRemove(key, out _);
            if (orderMessage?.Chat != null)
            {
                var orderKey = (orderMessage.Chat.Id, orderMessage.MessageId);
                _processedTakeOrderMessages.TryRemove(orderKey, out _);
                var deleteOrderBody = new { chat_id = orderMessage.Chat.Id, message_id = orderMessage.MessageId };
                var deleteOrderJson = Newtonsoft.Json.JsonConvert.SerializeObject(deleteOrderBody);
                using var deleteOrderContent = new StringContent(deleteOrderJson, System.Text.Encoding.UTF8, "application/json");
                await client.PostAsync(deleteUrl, deleteOrderContent, ct);
                var payload = await _orderPayloadStore.GetAsync(orderId, ct);
                var republished = false;
                if (payload != null && !string.IsNullOrEmpty(payload.Text))
                {
                    var chatId = !string.IsNullOrWhiteSpace(payload.ChatId) ? payload.ChatId : orderMessage.Chat.Id.ToString();
                    var sendBody = new { chat_id = chatId, text = payload.Text, parse_mode = payload.ParseMode ?? "HTML", reply_markup = payload.ReplyMarkup };
                    var sendUrl = $"{baseUrl}/sendMessage";
                    var sendJson = Newtonsoft.Json.JsonConvert.SerializeObject(sendBody, new Newtonsoft.Json.JsonSerializerSettings { NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore });
                    using var sendContent = new StringContent(sendJson, System.Text.Encoding.UTF8, "application/json");
                    await client.PostAsync(sendUrl, sendContent, ct);
                    republished = true;
                    _logger.LogInformation("Отказ от заказа: orderId={OrderId}, пользователь={User}, заказ переопубликован из Redis", orderId, userName);
                }
                else
                    _logger.LogInformation("Отказ от заказа: orderId={OrderId}, пользователь={User}, сообщения удалены (payload в Redis не найден)", orderId, userName);
                await _orderStatePublisher.PublishAsync(new OrderDeliveryStateMessage
                {
                    OrderId = orderId,
                    Status = OrderDeliveryStatus.Refused,
                    Timestamp = DateTime.UtcNow,
                    UserId = fromUserId,
                    UserName = userName,
                    Republished = republished
                }, ct);
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "Ошибка при отказе от заказа"); }
    }

    private async Task AnswerCallbackQueryAsync(string baseUrl, string callbackQueryId, string text, bool showAlert, CancellationToken ct)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            var answerUrl = $"{baseUrl}/answerCallbackQuery";
            var body = new { callback_query_id = callbackQueryId, text, show_alert = showAlert };
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(body);
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            await client.PostAsync(answerUrl, content, ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "answerCallbackQuery не удалось отправить"); }
    }
}
