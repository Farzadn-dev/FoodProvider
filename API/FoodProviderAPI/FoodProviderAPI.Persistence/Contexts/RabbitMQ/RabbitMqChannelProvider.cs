using FoodProviderAPI.Application.Contexts.RabbitMQ;
using FoodProviderAPI.Common.ResultDto;
using RabbitMQ.Client;

namespace FoodProviderAPI.Persistence.Contexts.RabbitMQ
{
    public class RabbitMqChannelProvider(IRabbitMqConnectionProvider connectionProvider) : IRabbitMqChannelProvider, IAsyncDisposable
    {
        private Dictionary<string, IChannel> _channels = [];

        public async Task<ResultDto<IChannel>> GetChannelAsync(string rabbitName, CancellationToken ct = default, CreateChannelOptions? options = null)
        {
            var result = await connectionProvider.GetConnectionAsync(rabbitName);

            if (!result.IsSuccess || result.Data == null || !result.Data.IsOpen)
                return ResultDto<IChannel>.InternalServerError("RabbitMQ Connection is not open");

            if (_channels.ContainsKey(rabbitName))
                return ResultDto<IChannel>.Success(_channels[rabbitName]);

            var channel = await result.Data.CreateChannelAsync(options, ct);
            _channels.Add(rabbitName, channel);

            return ResultDto<IChannel>.Success(channel);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var channel in _channels.Values)
            {
                await channel.CloseAsync();
                await channel.DisposeAsync();
            }
        }
    }
}
