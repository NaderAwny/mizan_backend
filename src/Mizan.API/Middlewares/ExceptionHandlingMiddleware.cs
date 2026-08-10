using System.Net;
using System.Text.Json;
using Mizan.Core.Exceptions;

namespace Mizan.API.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = exception switch
        {
            NotFoundException notFound => ((int)HttpStatusCode.NotFound, notFound.Message),
            BadRequestException badRequest => ((int)HttpStatusCode.BadRequest, badRequest.Message),
            DomainException domain => ((int)HttpStatusCode.BadRequest, domain.Message),
            UnauthorizedException unauthorized => ((int)HttpStatusCode.Unauthorized, unauthorized.Message),
            ForbiddenException forbidden => ((int)HttpStatusCode.Forbidden, forbidden.Message),
            _ => ((int)HttpStatusCode.InternalServerError, _env.IsDevelopment() ? exception.Message : "حدث خطأ غير متوقع في الخادم")
        };

        if (statusCode == (int)HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);
        }
        else
        {
            _logger.LogWarning("Handled business exception ({StatusCode}): {Message}", statusCode, message);
        }

        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.StatusCode = statusCode;

        var response = new
        {
            statusCode,
            message
        };

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
