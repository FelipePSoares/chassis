using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace FpsSoftware.Chassis.Tests;

public class CorrelationIdMiddlewareTests
{
    private static readonly RequestDelegate Next = _ => Task.CompletedTask;

    private static DefaultHttpContext CreateContext()
        => new() { TraceIdentifier = "trace-id" };

    private static CorrelationIdMiddleware CreateMiddleware(IDiagnosticContext? diagnosticContext = null)
        => new(Next, diagnosticContext ?? new DiagnosticContextFake());

    private sealed class DiagnosticContextFake : IDiagnosticContext
    {
        public IDictionary<string, object?> Values { get; } = new Dictionary<string, object?>();
        public void Set(string propertyName, object? value, bool destructureObjects = false) => Values[propertyName] = value;
        public void SetException(Exception exception) { }
    }

    [Fact]
    public async Task Invoke_WithoutExistingId_ShouldUseTraceIdentifier()
    {
        var context = CreateContext();
        var diagnostic = new DiagnosticContextFake();

        await CreateMiddleware(diagnostic).Invoke(context);

        context.Request.Headers["X-Correlation-Id"].Should().BeEquivalentTo("trace-id");
        context.Response.Headers["X-Correlation-Id"].Should().BeEquivalentTo("trace-id");
        context.Items["CorrelationId"].Should().Be("trace-id");
        diagnostic.Values["CorrelationId"].Should().Be("trace-id");
    }

    [Fact]
    public async Task Invoke_WithValidHeaderId_ShouldReuseIt()
    {
        var context = CreateContext();
        context.Request.Headers["X-Correlation-Id"] = "d4e1a7c2-8f1e-4f8b-9e9c-1a2b3c4d5e6f";

        await CreateMiddleware().Invoke(context);

        context.Response.Headers["X-Correlation-Id"].Should().BeEquivalentTo("d4e1a7c2-8f1e-4f8b-9e9c-1a2b3c4d5e6f");
    }

    [Fact]
    public async Task Invoke_WithInvalidHeaderId_ShouldFallBackToTraceIdentifier()
    {
        var context = CreateContext();
        context.Request.Headers["X-Correlation-Id"] = "not-a-guid";

        await CreateMiddleware().Invoke(context);

        context.Response.Headers["X-Correlation-Id"].Should().BeEquivalentTo("trace-id");
    }

    [Fact]
    public async Task Invoke_WithClaim_ShouldPreferClaimOverHeader()
    {
        var context = CreateContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("CorrelationId", "aaaa1111-2222-3333-4444-555566667777")]));

        await CreateMiddleware().Invoke(context);

        context.Response.Headers["X-Correlation-Id"].Should().BeEquivalentTo("aaaa1111-2222-3333-4444-555566667777");
    }
}
