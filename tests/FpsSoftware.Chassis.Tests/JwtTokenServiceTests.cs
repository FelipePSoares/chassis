using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;

namespace FpsSoftware.Chassis.Tests;

public class JwtTokenServiceTests
{
    private static JwtTokenSettings CreateSettings() => new()
    {
        SecretKey = Guid.NewGuid().ToString(),
        Issuer = "http://localhost:8080",
        Audience = "http://localhost:8080",
        TokenExpireSeconds = 300,
    };

    [Fact]
    public void CreateToken_ShouldReturnNonEmptyToken()
    {
        var token = JwtTokenService.CreateToken(CreateSettings(), [new Claim(ClaimTypes.NameIdentifier, "123")]);

        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CreateToken_ShouldIncludeIssuerAudienceAndExpiration()
    {
        var settings = CreateSettings();
        var token = JwtTokenService.CreateToken(settings, [new Claim("role", "admin")]);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Issuer.Should().Be(settings.Issuer);
        jwt.Audiences.Should().Contain(settings.Audience);
        jwt.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(settings.TokenExpireSeconds), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void CreateToken_ShouldIncludeProvidedClaims()
    {
        var token = JwtTokenService.CreateToken(
            CreateSettings(),
            [
                new Claim(ClaimTypes.NameIdentifier, "42"),
                new Claim(ClaimTypes.Role, "admin"),
            ]);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == "42");
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "admin");
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_ShouldReturnPrincipalDespiteExpiry()
    {
        var settings = CreateSettings();
        settings.TokenExpireSeconds = -10;
        var token = JwtTokenService.CreateToken(settings, [new Claim(ClaimTypes.NameIdentifier, "42")]);

        var principal = JwtTokenService.GetPrincipalFromExpiredToken(settings, token);

        principal.Should().NotBeNull();
        principal.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be("42");
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_WithWrongKey_ShouldThrow()
    {
        var settings = CreateSettings();
        var token = JwtTokenService.CreateToken(settings, [new Claim(ClaimTypes.NameIdentifier, "42")]);

        var wrongKeySettings = new JwtTokenSettings
        {
            SecretKey = Guid.NewGuid().ToString(),
            Issuer = settings.Issuer,
            Audience = settings.Audience,
        };

        Action act = () => JwtTokenService.GetPrincipalFromExpiredToken(wrongKeySettings, token);

        act.Should().Throw<SecurityTokenException>();
    }
}
