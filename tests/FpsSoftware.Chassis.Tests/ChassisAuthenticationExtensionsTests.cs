using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace FpsSoftware.Chassis.Tests;

public class ChassisAuthenticationExtensionsTests
{
    [Fact]
    public void AddChassisJwtAuthentication_ShouldRegisterAuthentication()
    {
        var services = new ServiceCollection();

        services.AddChassisJwtAuthentication(new JwtTokenSettings
        {
            Issuer = "issuer",
            Audience = "audience",
            SecretKey = new string('a', 32),
        });

        services.Should().Contain(d => d.ServiceType == typeof(IAuthenticationService));
    }
}
