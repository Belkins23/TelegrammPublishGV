using TelegrammPublishGV.Models;

namespace TelegrammPublishGV.Services;

public record OrderMessageLocation(string ChatId, int MessageId);

public interface IOrderPayloadStore
{
    Task SaveAsync(string orderId, PublishRequest payload, CancellationToken cancellationToken = default);
    Task<PublishRequest?> GetAsync(string orderId, CancellationToken cancellationToken = default);
    Task SaveMessageLocationAsync(string orderId, string chatId, int messageId, CancellationToken cancellationToken = default);
    Task<OrderMessageLocation?> GetMessageLocationAsync(string orderId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string orderId, CancellationToken cancellationToken = default);
}
