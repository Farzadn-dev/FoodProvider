using DbUpdater.Context;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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

var rabbitPort = Environment.GetEnvironmentVariable("RABBIT_PORT") ?? "5672";
var rabbitUserName = Environment.GetEnvironmentVariable("RABBIT_USER_NAME") ?? "guest";
var rabbitPassword = Environment.GetEnvironmentVariable("RABBIT_PASSWORD") ?? "guest";
#endregion

#region PostgreSQL Environment Variables
var PostgresHost = Environment.GetEnvironmentVariable("POSTGRES_HOST");
if (string.IsNullOrEmpty(PostgresHost))
{
    Console.WriteLine("POSTGRES_HOST environment variable is not set.");
    Environment.Exit(128);
}

var PostgresPort = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
var PostgresUser = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "postgres";
var PostgresPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "postgres";
var PostgresDatabase = Environment.GetEnvironmentVariable("POSTGRES_DATABASE") ?? "postgres";
#endregion

#region Redis Environment Variables
var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST_NAME");
if (string.IsNullOrEmpty(redisHost))
{
    Console.WriteLine("REDIS_HOST_NAME environment variable is not set.");
    Environment.Exit(128);
}

var redisPort = Environment.GetEnvironmentVariable("REDIS_PORT") ?? "6379";
var redisPassword = Environment.GetEnvironmentVariable("REDIS_PASSWORD") ?? "";
#endregion

#region Redis
var redis = ConnectionMultiplexer.Connect($"{redisHost}:{redisPort},password={redisPassword}");
StackExchange.Redis.IDatabase redisDb = redis.GetDatabase();
#endregion


#region PostgreSQL
var posConf = new PostgresConfiguration()
{
    PostgresHost = PostgresHost,
    PostgresPort = PostgresPort,
    PostgresUser = PostgresUser,
    PostgresPassword = PostgresPassword,
    PostgresDatabase = PostgresDatabase
};
using var db = new PostgresContext(posConf);
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
    var json = Encoding.UTF8.GetString(@event.Body.ToArray());
    var request = JsonSerializer.Deserialize<RabbitRequestDto<string>>(json);

    if (request == null || string.IsNullOrEmpty(request.Data))
    {
        await channel.BasicAckAsync(@event.DeliveryTag, multiple: false, cts.Token);
        return;
    }

    Console.WriteLine($"Received Request With Id '{request.Id}'");

    Console.WriteLine($"Updating Data With Id '{request.Id}' In PostgreSQL");
    var searchTags = await UpdateDataBaseAsync(db, request, cts);
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

    return;
}
#endregion

static async Task<string[]?> UpdateDataBaseAsync(PostgresContext db, RabbitRequestDto<string> request, CancellationTokenSource cts)
{
    var requestEntity = await db.SearchRequests.FindAsync(request.Id, cts.Token);
    if (requestEntity == null)
        return null;


    requestEntity.Status = DbUpdater.Domain.RequestStatus.Completed;
    requestEntity.FilePath = request.Data;
    requestEntity.FinishDate = DateTime.UtcNow;

    await db.SaveChangesAsync(cts.Token);
    return requestEntity.Tags;
}
async Task SetRedisCacheAsync(string[] searchTags, string path, Guid requestId, CancellationToken ct)
{
    string key = $"files:{HashSearchTags(searchTags)}";
    await redisDb.StringSetAsync(key, path, TimeSpan.FromMinutes(10));
}

string HashSearchTags(string[] searchTags)
{
    using (SHA256 sha256Hash = SHA256.Create())
    {
        // Create a new StringBuilder to collect the bytes and create a string.
        StringBuilder hash = new StringBuilder();

        // Concatenate all tags into a single string.
        string combinedTags = string.Join("", searchTags);

        // Compute the hash of the combined string.
        byte[] data = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(combinedTags));

        // Convert the byte array to a hexadecimal string.
        foreach (byte b in data)
        {
            hash.Append(b.ToString("x2"));
        }

        return hash.ToString();
    }
}