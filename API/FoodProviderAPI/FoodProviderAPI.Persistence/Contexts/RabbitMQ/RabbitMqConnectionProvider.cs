using FoodProviderAPI.Application.Contexts.RabbitMQ;
using FoodProviderAPI.Common.ResultDto;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using System.Collections.Concurrent;

namespace FoodProviderAPI.Persistence.Contexts.RabbitMQ
{
    public class RabbitMqConnectionProvider : IRabbitMqConnectionProvider, IAsyncDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly ConcurrentDictionary<string, IConnection> _connections = new();

        public RabbitMqConnectionProvider(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<ResultDto<IConnection>> GetConnectionAsync(string rabbitName, CancellationToken ct = default)
        {
            if (_connections.ContainsKey(rabbitName))
                return ResultDto<IConnection>.Success(_connections[rabbitName]);

            var section = _configuration.GetSection($"RabbitMQ:{rabbitName}");

            if (!section.Exists())
                return ResultDto<IConnection>.NotFound($"Section with name RabbitMQ:{rabbitName} NotFound in Settings");

            var factory = new ConnectionFactory
            {
                HostName = section["HostName"]!,
                UserName = section["UserName"]!,
                Password = section["Password"]!,
                Port = int.Parse(section["Port"]!)
            };
            var connection = await factory.CreateConnectionAsync(ct);
            bool addResult = _connections.TryAdd(rabbitName, connection);

            if (!addResult)
            {
                await connection.CloseAsync();
                await connection.DisposeAsync();

                return ResultDto<IConnection>.InternalServerError($"Failed to add connection to dictionary for {rabbitName}");
            }

            return ResultDto<IConnection>.Success(connection);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var conn in _connections.Values)
            {
                await conn.CloseAsync();
                await conn.DisposeAsync();
            }
            _connections.Clear();
        }
    }

}
