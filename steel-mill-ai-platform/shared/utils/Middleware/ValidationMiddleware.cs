using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SteelMillShared.Middleware;

public class ValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ValidationMiddleware> _logger;

    public ValidationMiddleware(RequestDelegate next, ILogger<ValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Validate content-type for POST/PUT requests
            if (context.Request.Method is "POST" or "PUT")
            {
                if (!IsValidContentType(context.Request.ContentType))
                {
                    context.Response.StatusCode = 415;
                    await context.Response.WriteAsync("Unsupported Media Type");
                    return;
                }
            }

            // Validate request size
            if (context.Request.ContentLength > 10_000_000) // 10MB limit
            {
                context.Response.StatusCode = 413;
                await context.Response.WriteAsync("Request too large");
                return;
            }

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in validation middleware");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static bool IsValidContentType(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return true;

        return contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) ||
               contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
               contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = 500;

        var response = new
        {
            error = "An error occurred while processing the request",
            details = exception.Message,
            timestamp = DateTime.UtcNow
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            
            _logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }
}