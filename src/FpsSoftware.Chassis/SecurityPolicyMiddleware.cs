using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace FpsSoftware.Chassis
{
    public class SecurityPolicyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly SecurityPolicyOptions _options;

        public SecurityPolicyMiddleware(RequestDelegate next, SecurityPolicyOptions options)
        {
            _next = next;
            _options = options;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments(_options.ApiPathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            if (Path.HasExtension(context.Request.Path))
            {
                await _next(context);
                return;
            }

            var bytes = new byte[16];
            RandomNumberGenerator.Fill(bytes);
            var nonce = Convert.ToBase64String(bytes);
            context.Items[_options.NonceKey] = nonce;

            var html = _options.FileProvider is not null
                ? await ReadIndexHtmlAsync(_options.FileProvider)
                : string.Empty;

            html = html.Replace(_options.NoncePlaceholder, nonce);

            context.Response.Headers.Append("Content-Security-Policy", _options.CspValue.Replace(_options.NoncePlaceholder, nonce));

            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(html);
        }

        private static async Task<string> ReadIndexHtmlAsync(IFileProvider fileProvider)
        {
            var file = fileProvider.GetFileInfo("wwwroot/index.html");
            if (!file.Exists)
                return string.Empty;

            using var stream = file.CreateReadStream();
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
    }

    public class SecurityPolicyOptions
    {
        public static SecurityPolicyOptions Default { get; } = new SecurityPolicyOptions();

        public string ApiPathPrefix { get; set; } = "/api";
        public string NonceKey { get; set; } = "CSP-Nonce";
        public string NoncePlaceholder { get; set; } = "{{nonce}}";
        public string CspValue { get; set; } = "default-src 'self'; script-src 'self' 'nonce-{{nonce}}';";
        public IFileProvider FileProvider { get; set; } = null!;
    }

    public static class SecurityPolicyMiddlewareExtensions
    {
        public static IApplicationBuilder UseSecurityPolicy(this IApplicationBuilder builder)
            => builder.UseMiddleware<SecurityPolicyMiddleware>(SecurityPolicyOptions.Default);

        public static IApplicationBuilder UseSecurityPolicy(this IApplicationBuilder builder, SecurityPolicyOptions options)
            => builder.UseMiddleware<SecurityPolicyMiddleware>(options);
    }
}
