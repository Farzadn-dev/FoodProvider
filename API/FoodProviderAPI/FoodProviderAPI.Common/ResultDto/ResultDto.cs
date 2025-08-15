
using System.Text.Json.Serialization;

namespace FoodProviderAPI.Common.ResultDto
{
    public sealed class ResultDto
    {
        [JsonConstructor]
        public ResultDto(bool isSuccess, StatusCode statusCode, string message)
        {
            IsSuccess = isSuccess;
            StatusCode = statusCode;
            Message = message;
        }

        public bool IsSuccess { get; init; }
        public StatusCode StatusCode { get; init; }
        public string Message { get; init; }

        public static ResultDto Success(StatusCode statusCode = StatusCode.OK, string message = "Done!") => new(true, statusCode, message);
        public static ResultDto Fail(StatusCode statusCode, string message) => new(false, statusCode, message);

        public static ResultDto NotFound(string message) => Fail(StatusCode.NotFound, message);
        public static ResultDto BadRequest(string message) => Fail(StatusCode.BadRequest, message);
        public static ResultDto Unauthorized(string message) => Fail(StatusCode.Unauthorized, message);
        public static ResultDto InternalServerError(string message) => Fail(StatusCode.InternalServerError, message);
    }

    public sealed class ResultDto<T>
    {
        [JsonConstructor]
        public ResultDto(T data, bool isSuccess, StatusCode statusCode, string message)
        {
            Data = data;

            IsSuccess = isSuccess;
            StatusCode = statusCode;
            Message = message;
        }

        private ResultDto(bool isSuccess, StatusCode statusCode, string message)
        {
            IsSuccess = isSuccess;
            StatusCode = statusCode;
            Message = message;
        }

        public bool IsSuccess { get; init; }
        public StatusCode StatusCode { get; init; }
        public string Message { get; init; }

        public T? Data { get; set; }

        public static ResultDto<T> Success(T data, StatusCode statusCode = StatusCode.OK, string message = "Done!") => new(true, statusCode, message) { Data = data };
        public static ResultDto<T> Fail(StatusCode statusCode, string message) => new(false, statusCode, message);

        public static ResultDto<T> NotFound(string message) => Fail(StatusCode.NotFound, message);
        public static ResultDto<T> BadRequest(string message) => Fail(StatusCode.BadRequest, message);
        public static ResultDto<T> Unauthorized(string message) => Fail(StatusCode.Unauthorized, message);
        public static ResultDto<T> InternalServerError(string message) => Fail(StatusCode.InternalServerError, message);
    }

    public enum StatusCode
    {
        OK = 200,
        NotFound = 404,
        BadRequest = 400,
        Unauthorized = 401,
        Forbidden = 403,
        InternalServerError = 500
    }
}
