using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace FpsSoftware.Chassis.Tests;

public class SecurityPolicyMiddlewareTests
{
    private static readonly RequestDelegate Next = _ => Task.CompletedTask;

    [Fact]
    public async Task Invoke_ApiRequest_ShouldPassThroughWithoutCsp()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/values";

        await new SecurityPolicyMiddleware(Next, SecurityPolicyOptions.Default).Invoke(context);

        context.Response.Headers.ContainsKey("Content-Security-Policy").Should().BeFalse();
    }

    [Fact]
    public async Task Invoke_StaticAsset_ShouldPassThroughWithoutCsp()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/main.js";

        await new SecurityPolicyMiddleware(Next, SecurityPolicyOptions.Default).Invoke(context);

        context.Response.Headers.ContainsKey("Content-Security-Policy").Should().BeFalse();
    }

    [Fact]
    public async Task Invoke_SpaRoute_ShouldServeIndexWithNonceAndCsp()
    {
        var fileProvider = new InMemoryFileProvider(new Dictionary<string, string>
        {
            ["wwwroot/index.html"] = "<html data-nonce=\"{{nonce}}\"></html>",
        });
        var options = new SecurityPolicyOptions
        {
            CspValue = "default-src 'self'; script-src 'self' 'nonce-{{nonce}}';",
            FileProvider = fileProvider,
        };
        var context = new DefaultHttpContext();
        context.Request.Path = "/dashboard";
        context.Response.Body = new MemoryStream();

        await new SecurityPolicyMiddleware(Next, options).Invoke(context);

        context.Response.ContentType.Should().Be("text/html");
        var body = ReadBody(context.Response.Body);
        var html = System.Text.Encoding.UTF8.GetString(body);
        html.Should().Contain("data-nonce=\"");
        html.Should().NotContain("{{nonce}}");

        var csp = context.Response.Headers["Content-Security-Policy"].ToString();
        csp.Should().Contain("nonce-");
        csp.Should().NotContain("{{nonce}}");
    }

    private static byte[] ReadBody(Stream body)
    {
        if (body is MemoryStream ms)
            return ms.ToArray();

        using var copy = new MemoryStream();
        body.CopyTo(copy);
        return copy.ToArray();
    }

    private sealed class InMemoryFileProvider : IFileProvider
    {
        private readonly Dictionary<string, string> files;
        public InMemoryFileProvider(Dictionary<string, string> files) => this.files = files;

        public IDirectoryContents GetDirectoryContents(string subpath) => new NotFoundDirectoryContents();
        public IFileInfo GetFileInfo(string subpath) =>
            files.TryGetValue(subpath, out var content) ? new VirtualFileInfo(subpath, content) : new NotFoundFileInfo(subpath);
        public IChangeToken Watch(string filter) => NullChangeToken.Singleton;
    }

    private sealed class VirtualFileInfo : IFileInfo
    {
        public VirtualFileInfo(string name, string content) { Name = name; Content = content; }
        public bool Exists => true;
        public long Length => System.Text.Encoding.UTF8.GetByteCount(Content);
        public string? PhysicalPath => null;
        public string Name { get; }
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public bool IsDirectory => false;
        public string Content { get; }
        public Stream CreateReadStream() => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Content));
    }
}
