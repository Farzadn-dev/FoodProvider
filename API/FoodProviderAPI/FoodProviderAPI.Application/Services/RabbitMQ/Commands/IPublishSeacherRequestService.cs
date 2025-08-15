

namespace FoodProviderAPI.Application.Services.RabbitMQ.Commands
{
    public interface IPublishSearchRequestService
    {
        Task<ResultDto> ExecuteAsync(RabbitMqRouteDto routeDto, RabbitRequestDto<string[]> requestDto, CancellationToken cancellationToken = default);
    }
    public class PublishSearchRequestService(IRabbitMqPublisher publisher) : IPublishSearchRequestService
    {
        public async Task<ResultDto> ExecuteAsync(RabbitMqRouteDto routeDto, RabbitRequestDto<string[]> requestDto, CancellationToken cancellationToken = default)
        {
            var result = await publisher.PublishMessageAsync(routeDto, requestDto, cancellationToken);
            return result;
        }
    }
}
