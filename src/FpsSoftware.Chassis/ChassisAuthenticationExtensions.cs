using System;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace FpsSoftware.Chassis
{
    public static class ChassisAuthenticationExtensions
    {
        /// <summary>
        /// Registers JWT bearer authentication using <see cref="JwtTokenSettings"/>.
        /// The consuming application supplies the issuer/audience/secret via
        /// configuration or environment variables, and is responsible for wiring
        /// its own user store (ASP.NET Identity or anything else).
        /// </summary>
        public static IServiceCollection AddChassisJwtAuthentication(
            this IServiceCollection services,
            JwtTokenSettings settings)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.RequireHttpsMetadata = true;
                    options.SaveToken = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = settings.ValidateIssuer && !string.IsNullOrEmpty(settings.Issuer),
                        ValidIssuer = settings.Issuer,
                        ValidateAudience = settings.ValidateAudience && !string.IsNullOrEmpty(settings.Audience),
                        ValidAudience = settings.Audience,
                        ValidateLifetime = settings.ValidateLifetime,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey)),
                        ClockSkew = TimeSpan.Zero,
                    };
                });

            return services;
        }
    }
}
