using DbUpdater.Context;
using DbUpdater.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

class Program
{
    static ManualResetEvent _exitEvent = new(false);
    private static IDatabase redisDb = default!;
    private static IChannel channel = default!;
    private static readonly CancellationTokenSource cts = new();

    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddDbContext<PostgresContext>();
                services.AddSingleton<IConfiguration>(context.Configuration);
            })
            .Build();

        // --- PostgreSQL ---
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PostgresContext>();
            await WaitForPostgresAsync(db);
        }

        // --- Environment variables ---
        var rabbitHostName = GetRequiredEnv("RABBIT_HOST_NAME");
        var rabbitRequestQueue = GetRequiredEnv("RABBIT_REQUEST_QUEUE");
        var rabbitPort = Environment.GetEnvironmentVariable("RABBIT_PORT") ?? "5672";
        var rabbitUserName = Environment.GetEnvironmentVariable("RABBIT_USER_NAME") ?? "guest";
        var rabbitPassword = Environment.GetEnvironmentVariable("RABBIT_PASSWORD") ?? "guest";

        var redisHost = GetRequiredEnv("REDIS_HOST_NAME");
        var redisPort = Environment.GetEnvironmentVariable("REDIS_PORT") ?? "6379";
        var redisPassword = Environment.GetEnvironmentVariable("REDIS_PASSWORD") ?? "";

        // --- Redis ---
        var redisConfig = $"{redisHost}:{redisPort},password={redisPassword}";
        var redis = await WaitForRedisAsync(redisConfig);
        redisDb = redis.GetDatabase();

        // --- RabbitMQ ---
        var connectionFactory = new ConnectionFactory
        {
            HostName = rabbitHostName,
            UserName = rabbitUserName,
            Password = rabbitPassword,
            Port = int.Parse(rabbitPort)
        };

        using var connection = await WaitForRabbitMQAsync(connectionFactory);
        channel = await connection.CreateChannelAsync(cancellationToken: cts.Token);
        await channel.QueueDeclareAsync(queue: rabbitRequestQueue);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (sender, @event) =>
        {
            await Consumer_ReceivedAsync(sender, @event, host.Services);
        };

        await channel.BasicConsumeAsync(
            queue: rabbitRequestQueue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cts.Token);

        Console.WriteLine("DbUpdater is working...");
        _exitEvent.WaitOne();
    }

    private static async Task Consumer_ReceivedAsync(object sender, BasicDeliverEventArgs @event, IServiceProvider services)
    {
        var json = Encoding.UTF8.GetString(@event.Body.ToArray());
        var request = JsonSerializer.Deserialize<RabbitRequestDto<string>>(json);

        if (request == null || string.IsNullOrEmpty(request.Data))
        {
            await channel.BasicAckAsync(@event.DeliveryTag, multiple: false, cts.Token);
            return;
        }

        Console.WriteLine($"Received Request With Id '{request.Id}'");
        Console.WriteLine($"Updating Data With Id '{request.Id}' In PostgreSQL");

        var searchTags = await UpdateDataBaseAsync(request, services);
        if (searchTags == null)
        {
            Console.WriteLine($"Request With Id '{request.Id}' Not Found In PostgreSQL");
            await channel.BasicAckAsync(@event.DeliveryTag, multiple: false, cts.Token);
            return;
        }

        Console.WriteLine($"Set Redis Cache For Tags Of Request Id '{request.Id}'");
        await SetRedisCacheAsync(searchTags, request.Data, request.Id, cts.Token);

        Console.WriteLine($"Acknowledging Request '{request.Id}'");
        await channel.BasicAckAsync(@event.DeliveryTag, multiple: false, cts.Token);

        Console.WriteLine($"Request '{request.Id}' Processed!");
    }

    private static async Task<string[]?> UpdateDataBaseAsync(RabbitRequestDto<string> request, IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgresContext>();

        var requestEntity = await db.SearchRequests.FindAsync(request.Id, cts.Token);
        if (requestEntity == null)
            return null;

        requestEntity.Status = RequestStatus.Completed;
        requestEntity.FilePath = request.Data;
        requestEntity.FinishDate = DateTime.UtcNow;

        await db.SaveChangesAsync(cts.Token);
        return requestEntity.Tags;
    }

    private static async Task SetRedisCacheAsync(string[] searchTags, string path, Guid requestId, CancellationToken ct)
    {
        string key = $"files:{HashSearchTags(searchTags)}";
        await redisDb.StringSetAsync(key, path, TimeSpan.FromMinutes(10));
    }

    private static string HashSearchTags(string[] searchTags)
    {
        using SHA256 sha256Hash = SHA256.Create();
        StringBuilder hash = new();
        string combinedTags = string.Join("", searchTags);
        byte[] data = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(combinedTags));

        foreach (byte b in data)
            hash.Append(b.ToString("x2"));

        return hash.ToString();
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

    // --- PostgreSQL retry ---
    public static async Task WaitForPostgresAsync(PostgresContext dbContext, int maxRetries = 10, int delaySeconds = 5)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await dbContext.Database.MigrateAsync();
                Console.WriteLine("Connected to PostgreSQL and migrations applied.");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PostgreSQL not ready (attempt {attempt}/{maxRetries}): {ex.Message}");
                if (attempt == maxRetries) throw;
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
        }
    }

    // --- RabbitMQ retry ---
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

    // --- Redis retry ---
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
