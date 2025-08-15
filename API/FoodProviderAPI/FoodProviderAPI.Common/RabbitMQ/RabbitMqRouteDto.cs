namespace FoodProviderAPI.Common.RabbitMQ
{
    public class RabbitMqRouteDto
    {
        public string RabbitName { get; set; } = default!;
        public string QueueName { get; set; } = default!;
    }
}
