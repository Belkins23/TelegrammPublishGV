using Newtonsoft.Json;

namespace TelegrammPublishGV.Models;

internal class TelegramSendMessageResponse
{
    [JsonProperty("ok")]
    public bool Ok { get; set; }

    [JsonProperty("result")]
    public TelegramMessageResult? Result { get; set; }
}

internal class TelegramMessageResult
{
    [JsonProperty("message_id")]
    public int MessageId { get; set; }
}
