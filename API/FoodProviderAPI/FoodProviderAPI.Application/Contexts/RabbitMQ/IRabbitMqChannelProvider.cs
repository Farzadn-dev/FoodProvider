using RabbitMQ.Client;

namespace FoodProviderAPI.Application.Contexts.RabbitMQ
{
    public interface IRabbitMqChannelProvider
    {
        Task<ResultDto<IChannel>> GetChannelAsync(string rabbitName, CancellationToken ct = default, CreateChannelOptions? options = null);
    }
}
