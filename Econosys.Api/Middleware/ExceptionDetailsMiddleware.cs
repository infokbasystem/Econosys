using System.Diagnostics;

namespace Econosys.Api.Middleware;

public class ExceptionDetailsMiddleware
{
    private const string GenericMessage = "Ett oväntat fel inträffade.";

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionDetailsMiddleware> _logger;
    private readonly IConfiguration _configuration;

    public ExceptionDetailsMiddleware(
        RequestDelegate next,
        ILogger<ExceptionDetailsMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (!context.RequestAborted.IsCancellationRequested)
        {
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

            _logger.LogError(
                ex,
                "Unhandled exception for {Method} {Path} (traceId {TraceId})",
                context.Request.Method,
                context.Request.Path,
                traceId);

            if (context.Response.HasStarted)
            {
                throw;
            }

            var includeDetails = _configuration.GetValue("ErrorHandling:IncludeExceptionDetails", false);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            if (!includeDetails)
            {
                await context.Response.WriteAsJsonAsync(
                    new
                    {
                        title = "Internal Server Error",
                        status = StatusCodes.Status500InternalServerError,
                        traceId,
                        message = GenericMessage,
                    },
                    options: null,
                    contentType: "application/problem+json");
                return;
            }

            var chain = new List<object>();
            Exception? innermost = ex;
            for (var current = ex; current is not null; current = current.InnerException)
            {
                chain.Add(new { type = current.GetType().FullName, message = current.Message });
                innermost = current;
            }

            await context.Response.WriteAsJsonAsync(
                new
                {
                    title = "Internal Server Error",
                    status = StatusCodes.Status500InternalServerError,
                    traceId,
                    message = innermost!.Message,
                    exceptionType = ex.GetType().FullName,
                    details = chain,
                    stackTrace = ex.ToString(),
                },
                options: null,
                contentType: "application/problem+json");
        }
    }
}
