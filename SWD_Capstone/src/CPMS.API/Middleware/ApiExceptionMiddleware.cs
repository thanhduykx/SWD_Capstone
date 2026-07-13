using CPMS.API.Contracts.Common;
using CPMS.BLL.Exceptions;
using CPMS.BLL.Common.Exceptions;
using CPMS.BLL.Models;
using FluentValidation;

namespace CPMS.API.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await next(context);
        }
        catch (BusinessRuleException exception)
        {
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, exception.Message, "business_rule");
        }
        catch (ValidationException exception)
        {
            var errors = exception.Errors
                .Select(error => new ApiError("validation", error.ErrorMessage, error.PropertyName))
                .ToArray();
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "Validation failed.", "validation", errors);
        }
        catch (ConflictException exception)
        {
            await WriteErrorAsync(context, StatusCodes.Status409Conflict, exception.Message, "conflict");
        }
        catch (UnauthorizedAccessException exception)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, exception.Message, "forbidden");
        }
        catch (ArgumentException exception)
        {
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, exception.Message, "invalid_argument");
        }
        catch (KeyNotFoundException exception)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, exception.Message, "not_found");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled API error.");
            await WriteErrorAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                "unexpected_error");
        }
    }

    private static async Task WriteErrorAsync(
        HttpContext context,
        int statusCode,
        string message,
        string code,
        IReadOnlyCollection<ApiError>? errors = null)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        var response = ApiResponse<object>.Fail(
            statusCode,
            message,
            context.TraceIdentifier,
            errors ?? [new ApiError(code, message)]);
        await context.Response.WriteAsJsonAsync(response);
    }
}
