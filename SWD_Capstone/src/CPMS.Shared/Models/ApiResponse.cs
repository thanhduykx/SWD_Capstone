namespace CPMS.Shared.Models;

public sealed record ApiError(string Code, string Message, string? Field = null);

public sealed record ApiResponse<T>(
    bool Success,
    int StatusCode,
    string Message,
    T? Data,
    IReadOnlyCollection<ApiError> Errors,
    string TraceId)
{
    public static ApiResponse<T> Ok(T data, string traceId, string message = "OK") =>
        new(true, 200, message, data, [], traceId);

    public static ApiResponse<T> Created(T data, string traceId, string message = "Created") =>
        new(true, 201, message, data, [], traceId);

    public static ApiResponse<T> Fail(
        int statusCode,
        string message,
        string traceId,
        IReadOnlyCollection<ApiError>? errors = null) =>
        new(false, statusCode, message, default, errors ?? [new ApiError("error", message)], traceId);
}
