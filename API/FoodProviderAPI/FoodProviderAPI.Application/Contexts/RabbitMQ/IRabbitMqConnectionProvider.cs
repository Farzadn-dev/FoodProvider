using RabbitMQ.Client;

namespace FoodProviderAPI.Application.Contexts.RabbitMQ
{
    public interface IRabbitMqConnectionProvider
    {
        Task<ResultDto<IConnection>> GetConnectionAsync(string rabbitNamel, CancellationToken ct = default);
    }

}
