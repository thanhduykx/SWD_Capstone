using System.Text;
using System.Text.Json;
using CPMS.BLL.Abstractions.Messaging;
using RabbitMQ.Client;

namespace CPMS.DAL.Messaging;

public sealed class RabbitMqMessageBus(IConnection connection) : IMessageBus
{
    public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        using IModel channel = connection.CreateModel();
        var exchange = typeof(TMessage).Name;
        channel.ExchangeDeclare(exchange, ExchangeType.Fanout, durable: true);
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        channel.BasicPublish(exchange, routingKey: string.Empty, basicProperties: null, body);
        return Task.CompletedTask;
    }
}
