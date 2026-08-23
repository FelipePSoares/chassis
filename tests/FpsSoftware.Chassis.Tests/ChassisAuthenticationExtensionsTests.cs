using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FpsSoftware.Chassis.Tests;

public class ChassisAuthenticationExtensionsTests
{
    private static JwtTokenSettings BuildSettings() => new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SecretKey = "super-secret-key-that-is-long-enough-for-hmac-sha256",
        TokenExpireSeconds = 3600,
        RefreshTokenExpireSeconds = 7200,
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true
    };

    [Fact]
    public void AddChassisJwtBearer_ShouldConfigureJwtBearer_FromTokenSettings()
    {
        var services = new ServiceCollection();

        services.AddChassisJwtBearer(BuildSettings());

        using var provider = services.BuildServiceProvider();
        var jwtOptions = provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        jwtOptions.SaveToken.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidIssuer.Should().Be("test-issuer");
        jwtOptions.TokenValidationParameters.ValidAudience.Should().Be("test-audience");
        jwtOptions.TokenValidationParameters.ValidateIssuer.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidateAudience.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidateLifetime.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidateIssuerSigningKey.Should().BeTrue();
        jwtOptions.TokenValidationParameters.IssuerSigningKey.Should().NotBeNull();
    }

    [Fact]
    public void AddChassisJwtBearer_ShouldRegisterJwtBearerEvents_WhenCookieNameProvided()
    {
        var services = new ServiceCollection();

        services.AddChassisJwtBearer(BuildSettings(), accessTokenCookieName: "AuthToken");

        using var provider = services.BuildServiceProvider();
        var jwtOptions = provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        jwtOptions.Events.Should().NotBeNull();
        jwtOptions.Events!.OnMessageReceived.Should().NotBeNull();
    }

    [Fact]
    public void AddChassisJwtBearer_ShouldInvokeConfigureJwtBearerCallback()
    {
        var services = new ServiceCollection();

        services.AddChassisJwtBearer(BuildSettings(), configureJwtBearer: options => options.SaveToken = false);

        using var provider = services.BuildServiceProvider();
        var jwtOptions = provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        jwtOptions.SaveToken.Should().BeFalse();
    }
}
