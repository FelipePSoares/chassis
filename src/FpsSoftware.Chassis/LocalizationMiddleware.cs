using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FpsSoftware.Chassis
{
    public class LocalizationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<LocalizationMiddleware> _logger;
        private readonly LocalizationOptions _options;

        public LocalizationMiddleware(RequestDelegate next, ILogger<LocalizationMiddleware> logger)
            : this(next, logger, new LocalizationOptions())
        {
        }

        public LocalizationMiddleware(RequestDelegate next, ILogger<LocalizationMiddleware> logger, LocalizationOptions options)
        {
            _next = next;
            _logger = logger;
            _options = options;
        }

        public async Task Invoke(HttpContext context)
        {
            var acceptLanguage = context.Request.Headers["Accept-Language"];
            var cultureSet = false;

            if (acceptLanguage.Count != 0)
            {
                foreach (var lang in acceptLanguage)
                {
                    var cultureCode = lang?.Split(';')[0].Trim();

                    try
                    {
                        var culture = new CultureInfo(cultureCode);
                        CultureInfo.CurrentCulture = culture;
                        CultureInfo.CurrentUICulture = culture;
                        cultureSet = true;
                        break;
                    }
                    catch (CultureNotFoundException)
                    {
                        continue;
                    }
                }
            }

            if (!cultureSet)
            {
                CultureInfo.CurrentCulture = new CultureInfo(_options.DefaultCulture);
                CultureInfo.CurrentUICulture = new CultureInfo(_options.DefaultCulture);
            }

            await _next(context);
        }
    }

    public class LocalizationOptions
    {
        public string DefaultCulture { get; set; } = "en-US";
    }

    public static class LocalizationMiddlewareExtensions
    {
        public static IApplicationBuilder UseLocationMiddleware(this IApplicationBuilder builder)
            => builder.UseMiddleware<LocalizationMiddleware>();

        public static IApplicationBuilder UseLocationMiddleware(this IApplicationBuilder builder, LocalizationOptions options)
            => builder.UseMiddleware<LocalizationMiddleware>(options);
    }
}
