using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace FpsSoftware.Chassis.Tests;

public class ExceptionMiddlewareTests
{
    private static readonly RequestDelegate Ok = ctx => { ctx.Response.StatusCode = 200; return Task.CompletedTask; };
    private static readonly RequestDelegate Throws = _ => throw new InvalidOperationException("boom");

    private static ExceptionMiddleware CreateMiddleware(IHostEnvironment env, RequestDelegate next)
        => new(next, env, NullLogger<ExceptionMiddleware>.Instance);

    [Fact]
    public async Task Invoke_WithoutException_ShouldPassThrough()
    {
        var context = new DefaultHttpContext();
        await CreateMiddleware(new HostEnvironmentFake { IsDevelopment = false }, Ok).InvokeAsync(context);
        context.Response.StatusCode.Should().Be(200);
    }

    [Theory]
    [InlineData(typeof(ArgumentException), HttpStatusCode.BadRequest)]
    [InlineData(typeof(UnauthorizedAccessException), HttpStatusCode.Unauthorized)]
    [InlineData(typeof(KeyNotFoundException), HttpStatusCode.NotFound)]
    [InlineData(typeof(InvalidOperationException), HttpStatusCode.InternalServerError)]
    public async Task Invoke_WithException_ShouldMapToStatusCode(Type exceptionType, HttpStatusCode expected)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "message")!;
        var context = new DefaultHttpContext();
        var middleware = new ExceptionMiddleware(
            _ => throw exception,
            new HostEnvironmentFake { IsDevelopment = false },
            NullLogger<ExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)expected);
    }

    [Fact]
    public async Task Invoke_InDevelopment_ShouldIncludeStackTraceAndType()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionMiddleware(
            Throws,
            new HostEnvironmentFake { IsDevelopment = true },
            NullLogger<ExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(500);
        context.Response.ContentType.Should().Be("application/json");

        using var document = JsonDocument.Parse(ReadBody(context.Response.Body));
        var root = document.RootElement;
        root.GetProperty("statusCode").GetInt32().Should().Be(500);
        root.GetProperty("type").GetString().Should().Be("InvalidOperationException");
        root.GetProperty("message").GetString().Should().Be("boom");
        root.GetProperty("stackTrace").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Invoke_InProduction_ShouldNotLeakExceptionDetail()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionMiddleware(
            Throws,
            new HostEnvironmentFake { IsDevelopment = false },
            NullLogger<ExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        var json = System.Text.Encoding.UTF8.GetString(ReadBody(context.Response.Body));
        json.Should().NotContain("boom");
        json.Should().NotContain("stackTrace");
    }

    private static byte[] ReadBody(Stream body)
    {
        if (body is MemoryStream ms)
            return ms.ToArray();

        using var copy = new MemoryStream();
        body.CopyTo(copy);
        return copy.ToArray();
    }

    private sealed class HostEnvironmentFake : IHostEnvironment
    {
        private string environmentName = string.Empty;
        public string EnvironmentName
        {
            get => environmentName;
            set => environmentName = value;
        }
        public bool IsDevelopment
        {
            get => environmentName == "Development";
            set => environmentName = value ? "Development" : "Production";
        }
        public string ApplicationName { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
