using FoodProviderAPI.Application.Services.RabbitMQ.Commands;

namespace FoodProviderAPI.Application.Services.RabbitMQ.Facade
{
    public interface IRabbitMqFacada
    {
        IPublishSearchRequestService PublishSearch { get; }
    }

    public class RabbitMqFacada(IRabbitMqPublisher publisher) : IRabbitMqFacada
    {
        private RabbitMqRouteDto searchQueue = new RabbitMqRouteDto(nameof(RabbitMQsEnum.RedisBroker), "SearchRequest");


        private PublishSearchRequestService? _publishSearch;
        public IPublishSearchRequestService PublishSearch => _publishSearch ??= new PublishSearchRequestService(publisher, searchQueue);
    }
}
