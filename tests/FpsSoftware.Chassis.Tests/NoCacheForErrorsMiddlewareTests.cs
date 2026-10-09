using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace FpsSoftware.Chassis.Tests;

public class NoCacheForErrorsMiddlewareTests
{
    private const string CacheableValue = "public, max-age=604800";

    private static NoCacheForErrorsMiddleware CreateMiddleware(RequestDelegate next, NoCacheForErrorsOptions? options = null)
        => new(next, options ?? new NoCacheForErrorsOptions());

    /// <summary>
    /// Builds a context for the pipeline. Note that DefaultHttpContext's response
    /// feature does not invoke OnStarting callbacks, which is exactly why the
    /// middleware also applies the headers synchronously after the pipeline runs —
    /// these tests exercise that path.
    /// </summary>
    private static DefaultHttpContext CreateContext() => new();

    [Fact]
    public async Task Invoke_WithErrorStatus_ShouldMakeResponseUncacheable()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(ctx =>
        {
            // Simulates an action-level [ResponseCache] attribute, which sets these
            // headers before the failing status code is known.
            ctx.Response.Headers["Cache-Control"] = CacheableValue;
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        context.Response.Headers["Cache-Control"].ToString()
            .Should().Contain("no-store").And.NotContain("max-age=604800");
        context.Response.Headers["Pragma"].ToString().Should().Be("no-cache");
        context.Response.Headers["Expires"].ToString().Should().Be("0");
    }

    [Theory]
    [InlineData(StatusCodes.Status404NotFound)]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status401Unauthorized)]
    [InlineData(StatusCodes.Status500InternalServerError)]
    [InlineData(StatusCodes.Status503ServiceUnavailable)]
    public async Task Invoke_WithAnyErrorStatus_ShouldSuppressCaching(int statusCode)
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.Headers["Cache-Control"] = CacheableValue;
            ctx.Response.StatusCode = statusCode;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        context.Response.Headers["Cache-Control"].ToString().Should().Contain("no-store");
    }

    [Fact]
    public async Task Invoke_WithSuccessStatus_ShouldPreserveCachingHeaders()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.Headers["Cache-Control"] = CacheableValue;
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        context.Response.Headers["Cache-Control"].ToString().Should().Be(CacheableValue);
        context.Response.Headers.ContainsKey("Pragma").Should().BeFalse();
    }

    [Fact]
    public async Task Invoke_WithErrorStatus_ShouldDropValidatorAndCdnCacheHeaders()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.Headers["ETag"] = "\"abc\"";
            ctx.Response.Headers["Last-Modified"] = "Wed, 01 Oct 2026 00:00:00 GMT";
            ctx.Response.Headers["Age"] = "122091";
            ctx.Response.Headers["Surrogate-Control"] = "max-age=604800";
            ctx.Response.Headers["CDN-Cache-Control"] = "max-age=604800";
            ctx.Response.Headers["Cloudflare-CDN-Cache-Control"] = "max-age=604800";
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        // A stale Age or a surviving validator would let a cache reuse the failure.
        foreach (var header in new[] { "ETag", "Last-Modified", "Age", "Surrogate-Control", "CDN-Cache-Control", "Cloudflare-CDN-Cache-Control" })
        {
            context.Response.Headers.ContainsKey(header).Should().BeFalse($$"""{{header}} must not survive on an error response""");
        }
    }

    [Fact]
    public async Task Invoke_WithExceptionThrown_ShouldSuppressCachingOnceHandlerSets500()
    {
        var context = CreateContext();

        // Mirrors the real pipeline: the exception propagates out of this middleware
        // to the outer exception handler, which then sets 500 and writes the body.
        // The OnStarting callback registered here is what must strip the headers at
        // that point. DefaultHttpContext does not invoke such callbacks, so the
        // callback is captured and invoked the way a real host would.
        Func<object, Task>? onStarting = null;
        var next = new RequestDelegate(ctx =>
        {
            ctx.Response.Headers["Cache-Control"] = CacheableValue;
            throw new InvalidOperationException("boom");
        });

        var middleware = CreateMiddleware(next);
        var features = context.Features;
        var originalFeature = features.Get<IHttpResponseFeature>();
        features.Set<IHttpResponseFeature>(new CallbackCapturingResponseFeature(originalFeature!, cb => onStarting = cb));

        var act = async () => await middleware.InvokeAsync(context);
        await act.Should().ThrowAsync<InvalidOperationException>();

        onStarting.Should().NotBeNull("the middleware must register an OnStarting callback");

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await onStarting!(context);

        context.Response.Headers["Cache-Control"].ToString().Should().Contain("no-store");
        context.Response.Headers["Cache-Control"].ToString().Should().NotContain("max-age=604800");
    }

    [Fact]
    public async Task Invoke_WithSubErrorStatus_ShouldNotSuppressCachingByDefault()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.Headers["Cache-Control"] = CacheableValue;
            ctx.Response.StatusCode = StatusCodes.Status304NotModified;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        context.Response.Headers["Cache-Control"].ToString().Should().Be(CacheableValue);
    }

    [Fact]
    public async Task Invoke_WithConfiguredThreshold_ShouldSuppressCachingFromThatStatus()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(
            ctx =>
            {
                ctx.Response.Headers["Cache-Control"] = CacheableValue;
                ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return Task.CompletedTask;
            },
            new NoCacheForErrorsOptions { MinimumStatusCode = 500 });

        await middleware.InvokeAsync(context);

        context.Response.Headers["Cache-Control"].ToString().Should().Contain("no-store");
    }

    [Fact]
    public async Task Invoke_WithExtraStatusCodes_ShouldSuppressThoseToo()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(
            ctx =>
            {
                ctx.Response.Headers["Cache-Control"] = CacheableValue;
                ctx.Response.StatusCode = StatusCodes.Status302Found;
                return Task.CompletedTask;
            },
            new NoCacheForErrorsOptions { ExtraStatusCodes = new[] { StatusCodes.Status302Found } });

        await middleware.InvokeAsync(context);

        context.Response.Headers["Cache-Control"].ToString().Should().Contain("no-store");
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrow()
    {
        var act = () => new NoCacheForErrorsMiddleware(_ => Task.CompletedTask, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Options_Default_ShouldUseErrorRangeThreshold()
    {
        NoCacheForErrorsOptions.Default.MinimumStatusCode.Should().Be(400);
        NoCacheForErrorsOptions.Default.ExtraStatusCodes.Should().BeNull();
    }

    /// <summary>
    /// Wraps the default response feature so registered OnStarting callbacks can be
    /// captured. DefaultHttpContext's own feature stores them but never invokes
    /// them, unlike the Kestrel feature used in production.
    /// </summary>
    private sealed class CallbackCapturingResponseFeature : IHttpResponseFeature
    {
        private readonly IHttpResponseFeature _inner;
        private readonly Action<Func<object, Task>> _capture;

        public CallbackCapturingResponseFeature(IHttpResponseFeature inner, Action<Func<object, Task>> capture)
        {
            _inner = inner;
            _capture = capture;
        }

        public int StatusCode
        {
            get => _inner.StatusCode;
            set => _inner.StatusCode = value;
        }

        public string? ReasonPhrase
        {
            get => _inner.ReasonPhrase;
            set => _inner.ReasonPhrase = value;
        }

        public IHeaderDictionary Headers
        {
            get => _inner.Headers;
            set => _inner.Headers = value;
        }

#pragma warning disable CS0618 // IHttpResponseFeature.Body is obsolete but still part of the interface.
        public Stream Body
        {
            get => _inner.Body;
            set => _inner.Body = value;
        }
#pragma warning restore CS0618

        public bool HasStarted => _inner.HasStarted;

        public void OnStarting(Func<object, Task> callback, object state)
        {
            _capture(callback);
            _inner.OnStarting(callback, state);
        }

        public void OnCompleted(Func<object, Task> callback, object state) => _inner.OnCompleted(callback, state);
    }
}
