using TelegrammPublishGV.Models;

namespace TelegrammPublishGV.Services;

public interface IOrderStatePublisher
{
    Task PublishAsync(OrderDeliveryStateMessage message, CancellationToken cancellationToken = default);
}
