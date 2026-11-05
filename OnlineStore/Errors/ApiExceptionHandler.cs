
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace OnlineStore.Errors;

public sealed class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        int statusCode = exception switch
        {
            BadHttpRequestException badRequest => badRequest.StatusCode,
            ValidationException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        bool isClientError = statusCode >= 400 && statusCode < 500;

        // Never log exception messages or submitted values.
        if (isClientError)
        {
            logger.LogWarning(
                "API request rejected. ExceptionType={ExceptionType}, TraceId={TraceId}",
                exception.GetType().Name,
                context.TraceIdentifier);
        }
        else
        {
            logger.LogError(
                "Unhandled API exception. ExceptionType={ExceptionType}, TraceId={TraceId}",
                exception.GetType().Name,
                context.TraceIdentifier);
        }

        string title = exception switch
        {
            BadHttpRequestException => "Invalid request.",
            ValidationException => "Request validation failed.",
            _ => "Internal server error."
        };

        await Results.Problem(
            statusCode: statusCode,
            title: title,
            detail: isClientError
                ? "The request could not be processed."
                : "The request could not be completed.",
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = context.TraceIdentifier
            }
        ).ExecuteAsync(context);

        return true;
    }
}
