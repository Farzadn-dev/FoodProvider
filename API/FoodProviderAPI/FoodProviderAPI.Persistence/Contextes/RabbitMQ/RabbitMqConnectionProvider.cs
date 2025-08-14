using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using System.Collections.Concurrent;

namespace FoodProviderAPI.Persistence.Contextes.RabbitMQ
{
    public interface IRabbitMqConnectionProvider
    {
        Task<IConnection> GetConnectionAsync(string rabbitName);
    }

    public class RabbitMqConnectionProvider : IRabbitMqConnectionProvider, IAsyncDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly ConcurrentDictionary<string, Task<IConnection>> _connections = new();

        public RabbitMqConnectionProvider(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<IConnection> GetConnectionAsync(string rabbitName)
        {
            var connectionTask = _connections.GetOrAdd(rabbitName, async name =>
            {
                var section = _configuration.GetSection($"RabbitMQ:{name}");

                if (!section.Exists())
                    throw new ArgumentNullException(nameof(section), $"No config for {name}");

                var factory = new ConnectionFactory
                {
                    HostName = section["HostName"]!,
                    UserName = section["UserName"]!,
                    Password = section["Password"]!,
                    Port = int.Parse(section["Port"]!)
                };

                return await factory.CreateConnectionAsync();
            });

            return await connectionTask;
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var task in _connections.Values)
            {
                var conn = await task;
                await conn.CloseAsync();
                await conn.DisposeAsync();
            }
            _connections.Clear();
        }
    }

}
