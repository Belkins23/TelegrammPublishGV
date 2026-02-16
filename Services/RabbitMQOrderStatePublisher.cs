using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using TelegrammPublishGV.Models;

namespace TelegrammPublishGV.Services;

public class RabbitMQOrderStatePublisher : IOrderStatePublisher, IDisposable
{
    private readonly RabbitMQStatesOptions _options;
    private readonly ILogger<RabbitMQOrderStatePublisher> _logger;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly object _lock = new();

    public RabbitMQOrderStatePublisher(
        IOptions<RabbitMQStatesOptions> options,
        ILogger<RabbitMQOrderStatePublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task PublishAsync(OrderDeliveryStateMessage message, CancellationToken cancellationToken = default)
    {
        if (!_options.Enable)
            return Task.CompletedTask;

        try
        {
            EnsureChannel();
            if (_channel == null)
                return Task.CompletedTask;

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(message);
            var body = Encoding.UTF8.GetBytes(json);
            _channel.BasicPublish(
                exchange: string.Empty,
                routingKey: _options.QueueName,
                body: body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось отправить сообщение состояния заказа {OrderId} в RabbitMQ", message.OrderId);
        }

        return Task.CompletedTask;
    }

    private void EnsureChannel()
    {
        if (_channel != null && _channel.IsOpen)
            return;
        lock (_lock)
        {
            if (_channel != null && _channel.IsOpen)
                return;
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _options.HostName,
                    Port = _options.Port,
                    UserName = _options.UserName,
                    Password = _options.Password
                };
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                _channel.QueueDeclare(_options.QueueName, durable: true, exclusive: false, autoDelete: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка подключения к RabbitMQ");
            }
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
