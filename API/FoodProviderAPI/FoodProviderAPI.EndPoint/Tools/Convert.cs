using FoodProviderAPI.Common.ResultDto;

namespace FoodProviderAPI.EndPoint.Tools
{
    public static class Convert
    {
        public static IResult ToHttpResult(this ResultDto result) => TypedResults.Json(result, statusCode: (int)result.StatusCode);
        public static IResult ToHttpResult<T>(this ResultDto<T> result) => TypedResults.Json(result, statusCode: (int)result.StatusCode);
    }
}
