using System.Text;
using System.Text.Json;
using InventoryService.Data;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace InventoryService.Messaging;

public class RabbitSubscriber : BackgroundService
{
    private readonly ILogger<RabbitSubscriber> _logger;
    private readonly IServiceProvider _sp;
    private readonly IConfiguration _cfg;

    public RabbitSubscriber(ILogger<RabbitSubscriber> logger, IServiceProvider sp, IConfiguration cfg)
    {
        _logger = logger;
        _sp = sp;
        _cfg = cfg;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory { HostName = _cfg["RabbitMQ:HostName"] ?? "rabbitmq" };
        var queue = _cfg["RabbitMQ:Queue"] ?? "order.confirmed";

        var connection = factory.CreateConnection();
        var channel = connection.CreateModel();
        channel.QueueDeclare(queue: queue, durable: false, exclusive: false, autoDelete: false, arguments: null);

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += async (_, ea) =>
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            try
            {
                var message = JsonSerializer.Deserialize<OrderConfirmedMessage>(json);
                if (message is null) return;

                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                foreach (var it in message.Items)
                {
                    var p = await db.Products.FirstOrDefaultAsync(x => x.Id == it.ProductId);
                    if (p is null) continue;
                    p.Quantity -= it.Quantity;
                    if (p.Quantity < 0) p.Quantity = 0;
                }
                await db.SaveChangesAsync();
                _logger.LogInformation("Stock updated from order message {@msg}", message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process message: {json}", json);
            }
        };

        channel.BasicConsume(queue: queue, autoAck: true, consumer: consumer);
        return Task.CompletedTask;
    }

    private record OrderConfirmedMessage(List<OrderItemMessage> Items);
    private record OrderItemMessage(int ProductId, int Quantity);
}
