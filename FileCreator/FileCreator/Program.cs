using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;
using System.Text;
using System.Text.Json;

class Program
{
    static ManualResetEvent _exitEvent = new ManualResetEvent(false);

    static async Task Main(string[] args)
    {
        string? outputPath = Environment.GetEnvironmentVariable("OUTPUT_PATH");
        if (string.IsNullOrEmpty(outputPath))
            throw new InvalidOperationException("OUTPUT_PATH environment variable is not set.");

        #region RabbitMQ Environment Variables
        var rabbitHostName = Environment.GetEnvironmentVariable("RABBIT_HOST_NAME")
                             ?? throw new InvalidOperationException("RABBIT_HOST_NAME is not set.");
        var rabbitRequestQueue = Environment.GetEnvironmentVariable("RABBIT_REQUEST_QUEUE")
                                ?? throw new InvalidOperationException("RABBIT_REQUEST_QUEUE is not set.");
        var rabbitResponseQueue = Environment.GetEnvironmentVariable("RABBIT_RESPONSE_QUEUE")
                                 ?? throw new InvalidOperationException("RABBIT_RESPONSE_QUEUE is not set.");

        var rabbitPort = Environment.GetEnvironmentVariable("RABBIT_PORT") ?? "5672";
        var rabbitUserName = Environment.GetEnvironmentVariable("RABBIT_USER_NAME") ?? "guest";
        var rabbitPassword = Environment.GetEnvironmentVariable("RABBIT_PASSWORD") ?? "guest";
        #endregion

        #region RabbitMQ Setup
        var cts = new CancellationTokenSource();
        var connectionFactory = new ConnectionFactory
        {
            HostName = rabbitHostName,
            UserName = rabbitUserName,
            Password = rabbitPassword,
            Port = int.Parse(rabbitPort)
        };

        using var connection = await WaitForRabbitMQAsync(connectionFactory);
        using var channel = await connection.CreateChannelAsync(cancellationToken: cts.Token);
        await channel.QueueDeclareAsync(rabbitRequestQueue);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (sender, @event) =>
        {
            await Consumer_ReceivedAsync(sender, @event, outputPath, rabbitResponseQueue, cts, channel);
        };

        await channel.BasicConsumeAsync(queue: rabbitRequestQueue,
                                        autoAck: false,
                                        consumer: consumer,
                                        cancellationToken: cts.Token);
        #endregion

        Console.WriteLine("FileCreator is working...");
        _exitEvent.WaitOne(); // Blocks the thread
    }

    private static async Task Consumer_ReceivedAsync(
        object sender,
        BasicDeliverEventArgs @event,
        string outputPath,
        string rabbitResponseQueue,
        CancellationTokenSource cts,
        IChannel channel)
    {
        string path = Path.Combine(outputPath, $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid()}.txt");

        try
        {
            var json = Encoding.UTF8.GetString(@event.Body.ToArray());
            var request = JsonSerializer.Deserialize<RabbitRequestDto<string[]>>(json);

            if (request?.Data == null || request.Data.Length == 0)
            {
                Console.WriteLine($"Data was null or empty. Acknowledging Request '{request?.Id}'");
                await channel.BasicAckAsync(@event.DeliveryTag, multiple: false, cts.Token);
                return;
            }

            Console.WriteLine($"Received Request With Id '{request.Id}'");
            Console.WriteLine($"Creating File '{path}' For Request '{request.Id}'");

            var text = new StringBuilder();
            foreach (var item in request.Data)
                text.AppendLine(item ?? "<null>");

            await File.WriteAllTextAsync(path, text.ToString());

            var response = new RabbitRequestDto<string>
            {
                Id = request.Id,
                Data = path
            };

            json = JsonSerializer.Serialize(response);
            var bytes = Encoding.UTF8.GetBytes(json);

            Console.WriteLine($"Publishing Response Of Request '{request.Id}'");
            await channel.BasicPublishAsync("", rabbitResponseQueue, bytes, cts.Token);

            Console.WriteLine($"Acknowledging Request '{request.Id}'");
            await channel.BasicAckAsync(@event.DeliveryTag, multiple: false, cts.Token);

            Console.WriteLine($"Request '{request.Id}' Processed!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing message: {ex.Message}");
            await channel.BasicNackAsync(@event.DeliveryTag, multiple: false, requeue: false, cts.Token);
        }
    }

    public static async Task<IConnection> WaitForRabbitMQAsync(ConnectionFactory factory, int maxRetries = 10, int delaySeconds = 5)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var connection = await factory.CreateConnectionAsync(CancellationToken.None);
                Console.WriteLine("Connected to RabbitMQ.");
                return connection;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RabbitMQ not ready (attempt {attempt}/{maxRetries}): {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
        }

        throw new Exception("Failed to connect to RabbitMQ after multiple attempts.");
    }
}