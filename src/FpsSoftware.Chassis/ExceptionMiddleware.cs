using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FpsSoftware.Chassis
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<ExceptionMiddleware> _logger;
        private readonly ExceptionMiddlewareOptions _options;

        public ExceptionMiddleware(RequestDelegate next, IHostEnvironment environment, ILogger<ExceptionMiddleware> logger)
            : this(next, environment, logger, new ExceptionMiddlewareOptions())
        {
        }

        public ExceptionMiddleware(RequestDelegate next, IHostEnvironment environment, ILogger<ExceptionMiddleware> logger, ExceptionMiddlewareOptions options)
        {
            _next = next;
            _environment = environment;
            _logger = logger;
            _options = options;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                var sanitizedPath = httpContext.Request.Path.Value?.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "");
                _logger.LogError(ex, "Unhandled exception occurred while processing request {Path}", sanitizedPath);
                await HandleExceptionAsync(httpContext, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogError("The response has already started, the exception middleware will not modify the response.");
                return;
            }

            context.Response.ContentType = "application/json";
            var statusCode = GetStatusCode(exception);
            context.Response.StatusCode = statusCode;

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(GetErrorDetails(exception, statusCode), options);
            await context.Response.WriteAsync(json);
        }

        private object GetErrorDetails(Exception exception, int statusCode)
        {
            if (_environment.IsDevelopment())
            {
                return new
                {
                    StatusCode = statusCode,
                    Message = exception.Message,
                    Type = exception.GetType().Name,
                    StackTrace = exception.StackTrace,
                    TraceId = Activity.Current?.Id ?? Guid.NewGuid().ToString(),
                    Path = Activity.Current?.OperationName,
                    Timestamp = DateTime.UtcNow
                };
            }

            return new
            {
                StatusCode = statusCode,
                Message = GetUserFriendlyMessage(exception),
                TraceId = Activity.Current?.Id ?? Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow
            };
        }

        private static int GetStatusCode(Exception exception) => exception switch
        {
            ArgumentException => (int)HttpStatusCode.BadRequest,
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            KeyNotFoundException => (int)HttpStatusCode.NotFound,
            _ => (int)HttpStatusCode.InternalServerError
        };

        private string GetUserFriendlyMessage(Exception exception) => exception switch
        {
            ArgumentException => _options.InvalidDataMessage,
            UnauthorizedAccessException => _options.UnauthorizedMessage,
            KeyNotFoundException => _options.NotFoundMessage,
            _ => _options.GenericErrorMessage
        };
    }

    public class ExceptionMiddlewareOptions
    {
        public string InvalidDataMessage { get; set; } = "Invalid data.";
        public string UnauthorizedMessage { get; set; } = "You don't have permission to perform this action.";
        public string NotFoundMessage { get; set; } = "Resource not found.";
        public string GenericErrorMessage { get; set; } = "An unexpected error occurred.";
    }

    public static class ExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseCustomExceptionHandler(this IApplicationBuilder builder)
            => builder.UseMiddleware<ExceptionMiddleware>();

        public static IApplicationBuilder UseCustomExceptionHandler(this IApplicationBuilder builder, ExceptionMiddlewareOptions options)
            => builder.UseMiddleware<ExceptionMiddleware>(options);
    }
}
