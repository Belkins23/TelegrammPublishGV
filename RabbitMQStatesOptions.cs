namespace TelegrammPublishGV;

public class RabbitMQStatesOptions
{
    public const string SectionName = "RabbitMQ_States";

    public bool Enable { get; set; }
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string QueueName { get; set; } = "order_delivery_states";
}
