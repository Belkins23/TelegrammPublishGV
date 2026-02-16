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

    public async Task SaveAsync(string orderId, PublishRequest payload, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = KeyPrefix + orderId;
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
        await db.StringSetAsync(key, json, DefaultTtl);
    }

    public async Task<PublishRequest?> GetAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = KeyPrefix + orderId;
        var json = await db.StringGetAsync(key);
        if (json.IsNullOrEmpty)
            return null;
        return Newtonsoft.Json.JsonConvert.DeserializeObject<PublishRequest>(json!);
    }

    public async Task SaveMessageLocationAsync(string orderId, string chatId, int messageId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = KeyPrefix + orderId + ":message";
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(new { ChatId = chatId, MessageId = messageId });
        await db.StringSetAsync(key, json, DefaultTtl);
    }

    public async Task<OrderMessageLocation?> GetMessageLocationAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var key = KeyPrefix + orderId + ":message";
        var json = await db.StringGetAsync(key);
        if (json.IsNullOrEmpty)
            return null;
        var obj = Newtonsoft.Json.JsonConvert.DeserializeObject<OrderMessageLocationDto>(json!);
        return obj != null ? new OrderMessageLocation(obj.ChatId ?? "", obj.MessageId) : null;
    }

    public async Task DeleteAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(KeyPrefix + orderId);
        await db.KeyDeleteAsync(KeyPrefix + orderId + ":message");
    }

    private class OrderMessageLocationDto
    {
        public string? ChatId { get; set; }
        public int MessageId { get; set; }
    }
}
