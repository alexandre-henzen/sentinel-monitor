using System.Diagnostics;

namespace EAM.API.Middleware;

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
        var stopwatch = Stopwatch.StartNew();
        
        _logger.LogInformation("Request started: {Method} {Path}", 
            context.Request.Method, context.Request.Path);

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            
            _logger.LogInformation("Request completed: {Method} {Path} - {StatusCode} in {Duration}ms",
                context.Request.Method, 
                context.Request.Path, 
                context.Response.StatusCode, 
                stopwatch.ElapsedMilliseconds);
        }
    }
}