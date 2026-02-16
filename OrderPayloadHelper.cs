using TelegrammPublishGV.Models;

namespace TelegrammPublishGV;

public static class OrderPayloadHelper
{
    /// <summary>Извлекает ID заказа из callback_data первой кнопки «Забрать заказ» (take_152 → 152).</summary>
    public static string? ExtractOrderId(PublishRequest? request)
    {
        var data = request?.ReplyMarkup?.InlineKeyboard
            ?.SelectMany(row => row)
            .FirstOrDefault(b => b.CallbackData?.StartsWith("take_", StringComparison.OrdinalIgnoreCase) == true)
            ?.CallbackData;

        if (string.IsNullOrEmpty(data) || data.Length <= 5)
            return null;
        return data[5..]; // "take_".Length == 5
    }
}
