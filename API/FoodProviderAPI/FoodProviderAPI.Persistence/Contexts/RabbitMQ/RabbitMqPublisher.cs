using FoodProviderAPI.Application.Contexts.RabbitMQ;
using FoodProviderAPI.Common.RabbitMQ;
using FoodProviderAPI.Common.ResultDto;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace FoodProviderAPI.Persistence.Contexts.RabbitMQ
{
    public class RabbitMqPublisher(IRabbitMqChannelProvider channelProvider) : IRabbitMqPublisher
    {
        private async Task<ResultDto> PublishMessageAsync(string rabbitName, string queueName, byte[] message, CancellationToken ct = default)
        {
            ResultDto<IChannel> result = await channelProvider.GetChannelAsync(rabbitName);
            if (!result.IsSuccess || result.Data == null || !result.Data.IsOpen)
                return ResultDto.NotFound($"Failed to get RabbitMQ channel for {rabbitName}");

            IChannel channel = result.Data;
            await channel.BasicPublishAsync("", queueName, message, ct);

            return ResultDto.Success();
        }

        private async Task<ResultDto> PublishMessageAsync(string rabbitName, string queueName, string message, CancellationToken ct = default)
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
            return await PublishMessageAsync(rabbitName, queueName, messageBytes, ct);
        }

        public async Task<ResultDto> PublishMessageAsync<T>(string rabbitName, string queueName, T message, CancellationToken ct = default)
        {
            var json = JsonSerializer.Serialize(message);
            if (json == null)
                return ResultDto.InternalServerError($"Failed to serialize message. rabbitName = {rabbitName}, queueName = {queueName}");

            return await PublishMessageAsync(rabbitName, queueName, json, ct);
        }

        public async Task<ResultDto> PublishMessageAsync<T>(RabbitMqRouteDto route, T messages, CancellationToken ct = default)
        {
            return await PublishMessageAsync(route.RabbitName, route.QueueName, messages, ct);
        }
    }
}
