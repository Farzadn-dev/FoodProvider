using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;
using System.Text;
using System.Text.Json;

class Program
{
    static ManualResetEvent _exitEvent = new ManualResetEvent(false);

    private static IDatabase redisDb = default!;
    private static IChannel channel = default!;
    private static CancellationTokenSource cts = new();

    static async Task Main(string[] args)
    {
        #region RabbitMQ Environment Variables
        var rabbitHostName = GetRequiredEnv("RABBIT_HOST_NAME");
        var rabbitRequestQueue = GetRequiredEnv("RABBIT_REQUEST_QUEUE");
        var rabbitResponseQueue = GetRequiredEnv("RABBIT_RESPONSE_QUEUE");
        var rabbitPort = Environment.GetEnvironmentVariable("RABBIT_PORT") ?? "5672";
        var rabbitUserName = Environment.GetEnvironmentVariable("RABBIT_USER_NAME") ?? "guest";
        var rabbitPassword = Environment.GetEnvironmentVariable("RABBIT_PASSWORD") ?? "guest";
        #endregion

        #region Redis Environment Variables
        var redisHost = GetRequiredEnv("REDIS_HOST_NAME");
        var redisPort = Environment.GetEnvironmentVariable("REDIS_PORT") ?? "6379";
        var redisPassword = Environment.GetEnvironmentVariable("REDIS_PASSWORD") ?? "";
        #endregion

        #region Redis
        var redisConfig = $"{redisHost}:{redisPort},password={redisPassword}";
        var redis = await WaitForRedisAsync(redisConfig);
        redisDb = redis.GetDatabase();
        #endregion

        #region RabbitMQ
        var connectionFactory = new ConnectionFactory
        {
            HostName = rabbitHostName,
            UserName = rabbitUserName,
            Password = rabbitPassword,
            Port = int.Parse(rabbitPort)
        };

        using var connection = await WaitForRabbitMQAsync(connectionFactory);
        channel = await connection.CreateChannelAsync(cancellationToken: cts.Token);
        await channel.QueueDeclareAsync(rabbitRequestQueue);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (sender, @event) =>
        {
            await Consumer_ReceivedAsync(sender, @event, rabbitResponseQueue);
        };

        await channel.BasicConsumeAsync(queue: rabbitRequestQueue,
                                        autoAck: false,
                                        consumer: consumer,
                                        cancellationToken: cts.Token);
        #endregion

        Console.WriteLine("DataReader is working...");
        _exitEvent.WaitOne();
    }

    private static async Task Consumer_ReceivedAsync(object sender, BasicDeliverEventArgs @event, string rabbitResponseQueue)
    {
        try
        {
            var json = Encoding.UTF8.GetString(@event.Body.ToArray());
            var request = JsonSerializer.Deserialize<RabbitRequestDto<string[]>>(json);

            if (request?.Data == null || request.Data.Length == 0)
            {
                Console.WriteLine($"Empty or null data. Acking message.");
                await channel.BasicAckAsync(@event.DeliveryTag, multiple: false, cts.Token);
                return;
            }

            Console.WriteLine($"Received Request With Id '{request.Id}'");

            RedisKey[] keys = request.Data.Select(x => (RedisKey)x).ToArray();

            Console.WriteLine($"Getting Set Members From Redis For Request '{request.Id}'...");
            RedisValue[] members = await redisDb.SetCombineAsync(SetOperation.Intersect, keys, CommandFlags.PreferReplica);

            if (members == null || members.Length == 0)
            {
                Console.WriteLine($"Redis Returned Null Or Empty Set Members For Request '{request.Id}'");
                await channel.BasicAckAsync(@event.DeliveryTag, multiple: false, cts.Token);
                return;
            }

            Console.WriteLine($"Fetching Actual Values For {members.Length} Members...");

            var values = members.Select(m => m.ToString()).ToArray();

            var response = new RabbitRequestDto<string[]>
            {
                Id = request.Id,
                Data = values
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

    private static string GetRequiredEnv(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrEmpty(value))
        {
            Console.WriteLine($"{key} environment variable is not set.");
            Environment.Exit(128);
        }
        return value!;
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

    public static async Task<ConnectionMultiplexer> WaitForRedisAsync(string configuration, int maxRetries = 10, int delaySeconds = 5)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var redis = await ConnectionMultiplexer.ConnectAsync(configuration);
                if (redis.IsConnected)
                {
                    Console.WriteLine("Connected to Redis.");
                    return redis;
                }

                Console.WriteLine($"Redis connection not established (attempt {attempt}/{maxRetries}).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Redis not ready (attempt {attempt}/{maxRetries}): {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        }

        throw new Exception("Failed to connect to Redis after multiple attempts.");
    }
}