

namespace FoodProviderAPI.Application.Services.RabbitMQ.Commands
{
    public interface IPublishSearchRequestService
    {
        Task<ResultDto> ExecuteAsync(RabbitRequestDto<string[]> requestDto, CancellationToken cancellationToken = default);
    }
    public class PublishSearchRequestService(IRabbitMqPublisher publisher, RabbitMqRouteDto routeDto) : IPublishSearchRequestService
    {
        public async Task<ResultDto> ExecuteAsync(RabbitRequestDto<string[]> requestDto, CancellationToken cancellationToken = default)
        {
            var result = await publisher.PublishMessageAsync(routeDto, requestDto, cancellationToken);
            return result;
        }
    }
}
