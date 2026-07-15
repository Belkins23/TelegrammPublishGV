using TelegrammPublishGV.Models;

namespace TelegrammPublishGV.Services;

public class RedisOrderPayloadStore : IOrderPayloadStore
{
    private const string KeyPrefix = "order:";
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromDays(30);

    private readonly StackExchange.Redis.IConnectionMultiplexer _redis;

    public RedisOrderPayloadStore(StackExchange.Redis.IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    // Ключи включают роль, чтобы сообщения менеджера и курьера для одного заказа
    // могли сосуществовать и удаляться независимо.
    private static string PayloadKey(string orderId, string role) => KeyPrefix + orderId + ":" + role;
    private static string MessageKey(string orderId, string role) => KeyPrefix + orderId + ":" + role + ":message";

    public async Task SaveAsync(string orderId, PublishRequest payload, string role = "courier", CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = PayloadKey(orderId, role);
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
        await db.StringSetAsync(key, json, DefaultTtl);
    }

    public async Task<PublishRequest?> GetAsync(string orderId, string role = "courier", CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = PayloadKey(orderId, role);
        var json = await db.StringGetAsync(key);
        if (json.IsNullOrEmpty)
            return null;
        return Newtonsoft.Json.JsonConvert.DeserializeObject<PublishRequest>(json!);
    }

    public async Task SaveMessageLocationAsync(string orderId, string chatId, int messageId, string role = "courier", CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = MessageKey(orderId, role);
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(new { ChatId = chatId, MessageId = messageId });
        await db.StringSetAsync(key, json, DefaultTtl);
    }

    public async Task<OrderMessageLocation?> GetMessageLocationAsync(string orderId, string role = "courier", CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = MessageKey(orderId, role);
        var json = await db.StringGetAsync(key);
        if (json.IsNullOrEmpty)
            return null;
        var obj = Newtonsoft.Json.JsonConvert.DeserializeObject<OrderMessageLocationDto>(json!);
        return obj != null ? new OrderMessageLocation(obj.ChatId ?? "", obj.MessageId) : null;
    }

    public async Task DeleteAsync(string orderId, string role = "courier", CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(PayloadKey(orderId, role));
        await db.KeyDeleteAsync(MessageKey(orderId, role));
    }

    private class OrderMessageLocationDto
    {
        public string? ChatId { get; set; }
        public int MessageId { get; set; }
    }
}
