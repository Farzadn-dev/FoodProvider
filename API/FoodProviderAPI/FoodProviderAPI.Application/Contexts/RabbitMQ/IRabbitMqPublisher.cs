namespace FoodProviderAPI.Application.Contexts.RabbitMQ
{
    public interface IRabbitMqPublisher
    {
        Task<ResultDto> PublishMessageAsync<T>(string rabbitName, string queueName, T message, CancellationToken ct = default);
        Task<ResultDto> PublishMessageAsync<T>(RabbitMqRouteDto route, T message, CancellationToken ct = default);
    }
}
