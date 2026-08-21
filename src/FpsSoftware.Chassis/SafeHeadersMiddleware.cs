using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace FpsSoftware.Chassis
{
    public static class SafeHeadersMiddleware
    {
        public static IApplicationBuilder UseSafeHeaders(this IApplicationBuilder builder, SafeHeadersOptions? options = null)
        {
            var opts = options ?? new SafeHeadersOptions();
            return builder.Use(async (context, next) =>
            {
                ApplyHeaders(context, opts);
                await next();
            });
        }

        public static void ApplyHeaders(HttpContext context, SafeHeadersOptions options)
        {
            context.Response.Headers.Append("Referrer-Policy", options.ReferrerPolicy);
            context.Response.Headers.Append("X-Content-Type-Options", options.XContentTypeOptions);
            context.Response.Headers.Append("X-Frame-Options", options.XFrameOptions);
            context.Response.Headers.Append("Permissions-Policy", options.PermissionsPolicy);
        }
    }

    public class SafeHeadersOptions
    {
        public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";
        public string XContentTypeOptions { get; set; } = "nosniff";
        public string XFrameOptions { get; set; } = "DENY";
        public string PermissionsPolicy { get; set; } = "geolocation=(self), microphone=(), camera=()";
    }
}
