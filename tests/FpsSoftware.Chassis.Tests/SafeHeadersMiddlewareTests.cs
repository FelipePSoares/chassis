using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace FpsSoftware.Chassis.Tests;

public class SafeHeadersMiddlewareTests
{
    [Fact]
    public void ApplyHeaders_ShouldAppendSecurityHeaders()
    {
        var context = new DefaultHttpContext();
        var options = new SafeHeadersOptions();

        SafeHeadersMiddleware.ApplyHeaders(context, options);

        context.Response.Headers["Referrer-Policy"].ToString().Should().Be("strict-origin-when-cross-origin");
        context.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
        context.Response.Headers["X-Frame-Options"].ToString().Should().Be("DENY");
        context.Response.Headers.ContainsKey("Permissions-Policy").Should().BeTrue();
    }

    [Fact]
    public void ApplyHeaders_WithOptions_ShouldApplyConfiguredValues()
    {
        var context = new DefaultHttpContext();
        var options = new SafeHeadersOptions { XFrameOptions = "SAMEORIGIN" };

        SafeHeadersMiddleware.ApplyHeaders(context, options);

        context.Response.Headers["X-Frame-Options"].ToString().Should().Be("SAMEORIGIN");
    }
}
