using TelegrammPublishGV.Models;

namespace TelegrammPublishGV;

public static class OrderPayloadHelper
{
    /// <summary>
    /// Извлекает ID заказа из callback_data первой кнопки «Забрать заказ» (take_152 → 152, курьер)
    /// или «Принять» (accept_152 → 152, менеджер).
    /// </summary>
    public static string? ExtractOrderId(PublishRequest? request)
    {
        var data = request?.ReplyMarkup?.InlineKeyboard
            ?.SelectMany(row => row)
            .FirstOrDefault(b =>
                b.CallbackData?.StartsWith("take_", StringComparison.OrdinalIgnoreCase) == true ||
                b.CallbackData?.StartsWith("accept_", StringComparison.OrdinalIgnoreCase) == true)
            ?.CallbackData;

        if (string.IsNullOrEmpty(data))
            return null;
        if (data.StartsWith("take_", StringComparison.OrdinalIgnoreCase))
            return data.Length > 5 ? data[5..] : null; // "take_".Length == 5
        if (data.StartsWith("accept_", StringComparison.OrdinalIgnoreCase))
            return data.Length > 7 ? data[7..] : null; // "accept_".Length == 7
        return null;
    }
}
