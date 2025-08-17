using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;
using System.Text;
using System.Text.Json;

string? outputPath = Environment.GetEnvironmentVariable("OUTPUT_PATH");

if (string.IsNullOrEmpty(outputPath))
{
    throw new InvalidOperationException("OUTPUT_PATH environment variable is not set.");
}



#region RabbitMQ Environment Variables
var rabbitHostName = Environment.GetEnvironmentVariable("RABBIT_HOST_NAME");
if (string.IsNullOrEmpty(rabbitHostName))
{
    Console.WriteLine("RABBIT_HOST_NAME environment variable is not set.");
    Environment.Exit(128);
}

var rabbitRequestQueue = Environment.GetEnvironmentVariable("RABBIT_REQUEST_QUEUE");
if (string.IsNullOrEmpty(rabbitRequestQueue))
{
    Console.WriteLine("RABBIT_REQUEST_QUEUE environment variable is not set.");
    Environment.Exit(128);
}

var rabbitResponseQueue = Environment.GetEnvironmentVariable("RABBIT_RESPONSE_QUEUE");
if (string.IsNullOrEmpty(rabbitResponseQueue))
{
    Console.WriteLine("RABBIT_RESPONSE_QUEUE environment variable is not set.");
    Environment.Exit(128);
}

var rabbitPort = Environment.GetEnvironmentVariable("RABBIT_PORT") ?? "5672";
var rabbitUserName = Environment.GetEnvironmentVariable("RABBIT_USER_NAME") ?? "guest";
var rabbitPassword = Environment.GetEnvironmentVariable("RABBIT_PASSWORD") ?? "guest";
#endregion


#region RabbitMQ
var cts = new CancellationTokenSource();
var connectionFactory = new ConnectionFactory()
{
    HostName = rabbitHostName,
    UserName = rabbitUserName,
    Password = rabbitPassword,
    Port = int.Parse(rabbitPort)
};

using var connection = await connectionFactory.CreateConnectionAsync(cts.Token);
using var channel = await connection.CreateChannelAsync(cancellationToken: cts.Token);

var consumer = new AsyncEventingBasicConsumer(channel);
consumer.ReceivedAsync += Consumer_ReceivedAsync;

await channel.BasicConsumeAsync(queue: rabbitRequestQueue,
                             autoAck: false,
                             consumer: consumer,
                             cancellationToken: cts.Token);

async Task Consumer_ReceivedAsync(object sender, BasicDeliverEventArgs @event)
{
    string path = Path.Combine(outputPath, Guid.NewGuid().ToString() + ".txt");

    var json = Encoding.UTF8.GetString(@event.Body.ToArray());
    var request = JsonSerializer.Deserialize<RabbitRequestDto<RedisValue[]>>(json);

    if (request == null || request.Data == null || request.Data.Length == 0)
    {
        await channel.BasicAckAsync(@event.DeliveryTag, multiple: false, cts.Token);
        return;
    }
    Console.WriteLine($"Received Request With Id '{request.Id}'");

    Console.WriteLine($"Creating File '{path}' For Request '{request.Id}'");
    StringBuilder Text = new();
    foreach (var item in request.Data)
        Text.AppendLine(item.ToString());

    using StreamWriter sw = File.CreateText(path);
    await sw.WriteLineAsync(Text.ToString());
    sw.Close();

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

    return;
}

#endregion
