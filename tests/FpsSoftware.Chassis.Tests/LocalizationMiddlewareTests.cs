using System.Globalization;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FpsSoftware.Chassis.Tests;

[Collection("Culture-Sensitive")]
public class LocalizationMiddlewareTests
{
    [Fact]
    public async Task Invoke_WithAcceptLanguage_ShouldSetCultureDownstream()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Accept-Language"] = "pt-BR";
        CultureInfo? seenDownstream = null;

        await new LocalizationMiddleware(
            _ => { seenDownstream = CultureInfo.CurrentCulture; return Task.CompletedTask; },
            NullLogger<LocalizationMiddleware>.Instance).Invoke(context);

        seenDownstream!.Name.Should().Be("pt-BR");
    }

    [Fact]
    public async Task Invoke_WithQualityValue_ShouldParseCultureCodeDownstream()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Accept-Language"] = "pt-BR;q=0.9";
        CultureInfo? seenDownstream = null;

        await new LocalizationMiddleware(
            _ => { seenDownstream = CultureInfo.CurrentCulture; return Task.CompletedTask; },
            NullLogger<LocalizationMiddleware>.Instance).Invoke(context);

        seenDownstream!.Name.Should().Be("pt-BR");
    }

    [Fact]
    public async Task Invoke_WithInvalidLanguage_ShouldFallBackToDefaultDownstream()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["Accept-Language"] = "xx-INVALID";
        CultureInfo? seenDownstream = null;

        await new LocalizationMiddleware(
            _ => { seenDownstream = CultureInfo.CurrentCulture; return Task.CompletedTask; },
            NullLogger<LocalizationMiddleware>.Instance).Invoke(context);

        seenDownstream!.Name.Should().Be("en-US");
    }

    [Fact]
    public async Task Invoke_WithoutAcceptLanguage_ShouldFallBackToDefaultDownstream()
    {
        var context = new DefaultHttpContext();
        CultureInfo? seenDownstream = null;

        await new LocalizationMiddleware(
            _ => { seenDownstream = CultureInfo.CurrentCulture; return Task.CompletedTask; },
            NullLogger<LocalizationMiddleware>.Instance).Invoke(context);

        seenDownstream!.Name.Should().Be("en-US");
    }
}
