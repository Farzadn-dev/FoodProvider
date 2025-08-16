namespace FoodProviderAPI.Common.RabbitMQ
{
    public class RabbitMqRouteDto
    {
        public string RabbitName { get; set; } = default!;
        public string QueueName { get; set; } = default!;

        public RabbitMqRouteDto()
        {

        }

        public RabbitMqRouteDto(string rabbitName, string queueName)
        {
            RabbitName = rabbitName;
            QueueName = queueName;
        }
    }
}
