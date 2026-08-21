using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
                    if (string.IsNullOrEmpty(cultureCode))
                        continue;

                    if (TrySetCulture(cultureCode))
                    {
                        cultureSet = true;
                        break;
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

        private static bool TrySetCulture(string cultureCode)
        {
            try
            {
                // `new CultureInfo(...)` accepts arbitrary codes as custom cultures
                // without throwing, so check it is a known culture before using it.
                if (!CultureInfo.GetCultures(CultureTypes.AllCultures)
                    .Any(c => string.Equals(c.Name, cultureCode, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                var culture = new CultureInfo(cultureCode);
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;
                return true;
            }
            catch (CultureNotFoundException)
            {
                return false;
            }
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
