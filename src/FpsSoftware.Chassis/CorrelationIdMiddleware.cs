using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace FpsSoftware.Chassis
{
    public class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IDiagnosticContext _diagnosticContext;
        private readonly CorrelationIdOptions _options;

        public CorrelationIdMiddleware(RequestDelegate next, IDiagnosticContext diagnosticContext)
            : this(next, diagnosticContext, new CorrelationIdOptions())
        {
        }

        public CorrelationIdMiddleware(RequestDelegate next, IDiagnosticContext diagnosticContext, CorrelationIdOptions options)
        {
            _next = next;
            _diagnosticContext = diagnosticContext;
            _options = options;
        }

        public async Task Invoke(HttpContext context)
        {
            var correlationId = context.User.Claims
                .FirstOrDefault(c => c.Type == _options.ClaimType)?.Value;

            if (string.IsNullOrEmpty(correlationId) && TryGetValidatedCorrelationIdFromHeader(context, out var headerCorrelationId))
                correlationId = headerCorrelationId;

            if (string.IsNullOrEmpty(correlationId))
                correlationId = context.TraceIdentifier;

            if (string.IsNullOrEmpty(correlationId))
                correlationId = Guid.NewGuid().ToString();

            context.Request.Headers[_options.HeaderName] = correlationId;
            context.Response.Headers[_options.HeaderName] = correlationId;
            context.Items[_options.ClaimType] = correlationId;
            _diagnosticContext.Set(_options.ClaimType, correlationId);

            using (Serilog.Context.LogContext.PushProperty(_options.ClaimType, correlationId))
            {
                await _next(context);
            }
        }

        private bool TryGetValidatedCorrelationIdFromHeader(HttpContext context, out string? correlationId)
        {
            correlationId = null;
            var headerValue = context.Request.Headers[_options.HeaderName].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(headerValue) || headerValue.Length > 64)
                return false;

            if (!Guid.TryParse(headerValue, out var parsedGuid))
                return false;

            correlationId = parsedGuid.ToString();
            return true;
        }
    }

    public class CorrelationIdOptions
    {
        public string ClaimType { get; set; } = "CorrelationId";
        public string HeaderName { get; set; } = "X-Correlation-Id";
    }

    public static class CorrelationIdMiddlewareExtensions
    {
        public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
            => builder.UseMiddleware<CorrelationIdMiddleware>();

        public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder, CorrelationIdOptions options)
            => builder.UseMiddleware<CorrelationIdMiddleware>(options);
    }
}
