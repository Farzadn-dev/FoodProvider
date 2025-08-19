using FoodProviderAPI.Application.Services.RabbitMQ.Facade;
using FoodProviderAPI.Common.RabbitMQ;
using FoodProviderAPI.EndPoint.Tools;
using Microsoft.AspNetCore.Mvc;

namespace FoodProviderAPI.EndPoint.Routes
{
    public static class FoodRoutes
    {
        public static void MapFoodRoutes(this IEndpointRouteBuilder app, string tags)
        {
            var route = app.MapGroup("/api/v1/food");

            route
                .MapPost("search", Search)
                .WithName("Search")
                .WithTags(tags);

        }

        private static async Task<IResult> Search([FromBody] string[] tags, IRabbitMqFacada rabbitMq, CancellationToken ct)
        {
            var request = new RabbitRequestDto<string[]>(tags);
            var result = await rabbitMq.PublishSearch.ExecuteAsync(request, ct);

            return result.ToHttpResult();
        }
    }
}
