using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace FpsSoftware.Chassis
{
    /// <summary>
    /// Stops shared caches (CDNs, reverse proxies, browsers) from storing error
    /// responses.
    /// <para>
    /// An endpoint that advertises a long <c>Cache-Control</c> lifetime — e.g. an
    /// image action decorated with <c>[ResponseCache(Duration = 604800)]</c> —
    /// applies that header regardless of the status code it ends up producing. A
    /// CDN such as Cloudflare caches a <c>500</c> reported by that endpoint for the
    /// full advertised lifetime, so a single transient failure at the origin is
    /// replayed to every visitor for days even after the application recovers.
    /// Requests served from the cache never reach the application, which also makes
    /// the failure invisible in the application logs.
    /// </para>
    /// <para>
    /// This middleware runs before the response is generated and rewrites the
    /// caching headers of every response whose status code is outside the
    /// successful range, so a failure is never cacheable. Successful responses are
    /// left untouched, preserving the intended caching for real content.
    /// </para>
    /// </summary>
    public class NoCacheForErrorsMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly NoCacheForErrorsOptions _options;

        public NoCacheForErrorsMiddleware(RequestDelegate next, NoCacheForErrorsOptions options)
        {
            _next = next;
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // The status code is only known once the rest of the pipeline has run, so
            // the headers cannot be decided up front. Two complementary hooks are
            // used, because neither alone is sufficient:
            //
            //  * OnStarting runs for responses that have not been written yet — the
            //    exception handler sets 500 and writes the body after this middleware
            //    returns, so this is the only chance to strip headers from it.
            //  * The post-await pass below covers responses that were fully written
            //    during _next (headers are still mutable while the response has not
            //    started) and hosts whose response feature does not invoke OnStarting
            //    callbacks at all.
            context.Response.OnStarting(state =>
            {
                ApplyNoCacheHeaders((HttpContext)state);
                return Task.CompletedTask;
            }, context);

            await _next(context);

            // Guarded: once the response has started its headers are sent, so
            // mutating them would throw.
            if (!context.Response.HasStarted)
                ApplyNoCacheHeaders(context);
        }

        /// <summary>
        /// Rewrites the caching headers of an error response so no shared cache can
        /// store it. Existing values are dropped rather than overwritten: a stale
        /// <c>Age</c> or a surviving validator (<c>ETag</c>/<c>Last-Modified</c>) is
        /// enough for a CDN to consider a cached failure reusable.
        /// </summary>
        private void ApplyNoCacheHeaders(HttpContext context)
        {
            if (!ShouldSuppressCaching(context.Response.StatusCode))
                return;

            var headers = context.Response.Headers;

            // IDictionary.Remove is used rather than the RemoveIfExist extension,
            // which lives in Microsoft.AspNetCore.Http.Extensions and would add a
            // package dependency this library does not need.
            headers.Remove("Cache-Control");
            headers.Remove("Pragma");
            headers.Remove("Expires");
            headers.Remove("Age");
            headers.Remove("ETag");
            headers.Remove("Last-Modified");
            headers.Remove("Vary");
            headers.Remove("Surrogate-Control");
            headers.Remove("CDN-Cache-Control");
            headers.Remove("Cloudflare-CDN-Cache-Control");

            headers["Cache-Control"] = NoStoreValue;
            headers["Pragma"] = "no-cache";
            headers["Expires"] = "0";
        }

        private bool ShouldSuppressCaching(int statusCode)
        {
            if (statusCode >= _options.MinimumStatusCode)
                return true;

            return _options.ExtraStatusCodes is not null
                && Array.IndexOf(_options.ExtraStatusCodes, statusCode) >= 0;
        }

        /// <summary>
        /// The header value sent on failures. <c>no-store</c> alone is enough for
        /// conforming caches; the additions also defeat the "revalidate, then reuse
        /// when the origin is unreachable" behaviour some CDNs fall back to.
        /// </summary>
        private const string NoStoreValue = "no-store, no-cache, must-revalidate, max-age=0";
    }

    public class NoCacheForErrorsOptions
    {
        public static NoCacheForErrorsOptions Default { get; } = new NoCacheForErrorsOptions();

        /// <summary>
        /// Status codes at or above this value are treated as errors and made
        /// uncacheable. Defaults to 400, covering both client and server errors,
        /// since a 404 for a missing object is just as wrong to pin at the edge as
        /// a 500.
        /// </summary>
        public int MinimumStatusCode { get; set; } = 400;

        /// <summary>
        /// Additional status codes below <see cref="MinimumStatusCode"/> that must
        /// also not be cached, for endpoints that use e.g. 3xx redirects or 2xx
        /// partial results as failure signals. Empty by default.
        /// </summary>
        public int[]? ExtraStatusCodes { get; set; }
    }

    public static class NoCacheForErrorsMiddlewareExtensions
    {
        /// <summary>
        /// Adds middleware that strips caching headers from every error response,
        /// so a CDN can never pin a transient failure. Register it early, before
        /// anything that can produce a response (the exception handler, static
        /// files, endpoints).
        /// </summary>
        public static IApplicationBuilder UseNoCacheForErrors(this IApplicationBuilder builder)
            => builder.UseMiddleware<NoCacheForErrorsMiddleware>(NoCacheForErrorsOptions.Default);

        /// <summary>
        /// Adds middleware that strips caching headers from every error response,
        /// so a CDN can never pin a transient failure. Register it early, before
        /// anything that can produce a response (the exception handler, static
        /// files, endpoints).
        /// </summary>
        public static IApplicationBuilder UseNoCacheForErrors(this IApplicationBuilder builder, NoCacheForErrorsOptions options)
            => builder.UseMiddleware<NoCacheForErrorsMiddleware>(options ?? throw new ArgumentNullException(nameof(options)));
    }
}
