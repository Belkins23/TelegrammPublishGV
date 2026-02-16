using Newtonsoft.Json;

namespace TelegrammPublishGV.Models;

/// <summary>Сообщение в очередь RabbitMQ о смене состояния заказа.</summary>
public class OrderDeliveryStateMessage
{
    [JsonProperty("orderId")]
    public string OrderId { get; set; } = string.Empty;

    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonProperty("messageId", NullValueHandling = NullValueHandling.Ignore)]
    public int? MessageId { get; set; }

    [JsonProperty("chatId", NullValueHandling = NullValueHandling.Ignore)]
    public string? ChatId { get; set; }

    [JsonProperty("userId", NullValueHandling = NullValueHandling.Ignore)]
    public long? UserId { get; set; }

    [JsonProperty("userName", NullValueHandling = NullValueHandling.Ignore)]
    public string? UserName { get; set; }

    [JsonProperty("republished", NullValueHandling = NullValueHandling.Ignore)]
    public bool? Republished { get; set; }
}

public static class OrderDeliveryStatus
{
    public const string Published = "Published";
    public const string Taken = "Taken";
    public const string Delivered = "Delivered";
    public const string Refused = "Refused";
    public const string Deleted = "Deleted";
}
