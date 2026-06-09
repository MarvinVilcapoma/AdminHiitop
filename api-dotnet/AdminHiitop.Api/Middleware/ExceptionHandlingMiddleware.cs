using System.Net;
using System.Text.Json;
using AdminHiitop.Api.Shared.Exceptions;

namespace AdminHiitop.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException exception)
        {
            await WriteErrorAsync(context, exception.StatusCode, exception.Message);
        }
        catch (AggregateException aggEx)
        {
            // Task.WhenAll with multiple failing tasks wraps them in AggregateException.
            // Unwrap to the first meaningful inner exception.
            Exception inner = aggEx.InnerExceptions.OfType<AppException>().FirstOrDefault()
                ?? aggEx.InnerException
                ?? aggEx;

            if (inner is AppException appEx)
            {
                await WriteErrorAsync(context, appEx.StatusCode, appEx.Message);
                return;
            }

            _logger.LogError(aggEx, "Unhandled aggregate exception");
            string aggMessage = "Ocurrio un error interno inesperado.";
            if (_environment.IsDevelopment())
                aggMessage = string.Join(" | ", aggEx.InnerExceptions.Select(e => e.Message));
            await WriteErrorAsync(context, (int)HttpStatusCode.InternalServerError, aggMessage);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception");
            string message = "Ocurrio un error interno inesperado.";

            if (_environment.IsDevelopment())
            {
                message = exception.InnerException is null
                    ? exception.Message
                    : $"{exception.Message} | Inner: {exception.InnerException.Message}";
            }

            await WriteErrorAsync(context, (int)HttpStatusCode.InternalServerError, message);
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            success = false,
            message
        }));
    }
}
