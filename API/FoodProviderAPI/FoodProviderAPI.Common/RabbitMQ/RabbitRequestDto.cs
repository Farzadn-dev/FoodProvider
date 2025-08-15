namespace FoodProviderAPI.Common.RabbitMQ
{
    public sealed class RabbitRequestDto<T>
    {
        public Guid Id { get; set; } = Guid.CreateVersion7();
        public required T Data { get; set; }
    }
}
