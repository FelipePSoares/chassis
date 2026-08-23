using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace FpsSoftware.Chassis
{
    public static class ChassisAuthenticationExtensions
    {
        /// <summary>
        /// Registers JWT bearer authentication configured from <see cref="JwtTokenSettings"/>.
        /// Application-specific concerns (ASP.NET Identity stores, roles, claims factory,
        /// API endpoint identity registration, cookie names) are supplied by the consuming
        /// application: Identity via its own <c>AddIdentityCore</c>/<c>AddIdentity</c> call,
        /// and bearer tweaks via <paramref name="configureJwtBearer"/>.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="jwtTokenSettings">Issuer/audience/secret/lifetime configuration.</param>
        /// <param name="accessTokenCookieName">
        /// Optional cookie name used as a fallback when the Authorization header is absent.
        /// When null/no cookie, only the standard Bearer header is respected.
        /// </param>
        /// <param name="configureJwtBearer">Optional hook to further customise the JwtBearer options.</param>
        public static IServiceCollection AddChassisJwtBearer(
            this IServiceCollection services,
            JwtTokenSettings jwtTokenSettings,
            string? accessTokenCookieName = null,
            Action<JwtBearerOptions>? configureJwtBearer = null)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (jwtTokenSettings == null) throw new ArgumentNullException(nameof(jwtTokenSettings));

            services.AddSingleton(jwtTokenSettings);

            services
                .AddAuthentication(config =>
                {
                    config.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    config.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                    config.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(config =>
                {
                    config.RequireHttpsMetadata = true;
                    config.SaveToken = true;
                    config.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtTokenSettings.SecretKey)),
                        ValidateIssuer = jwtTokenSettings.ValidateIssuer && !string.IsNullOrEmpty(jwtTokenSettings.Issuer),
                        ValidIssuer = jwtTokenSettings.Issuer,
                        ValidateAudience = jwtTokenSettings.ValidateAudience && !string.IsNullOrEmpty(jwtTokenSettings.Audience),
                        ValidAudience = jwtTokenSettings.Audience,
                        ValidateLifetime = jwtTokenSettings.ValidateLifetime,
                        ClockSkew = TimeSpan.Zero
                    };

                    if (!string.IsNullOrEmpty(accessTokenCookieName))
                    {
                        config.Events = new JwtBearerEvents
                        {
                            OnMessageReceived = context =>
                            {
                                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                                    context.Token = authHeader.Substring("Bearer ".Length).Trim();
                                else if (context.Request.Cookies.ContainsKey(accessTokenCookieName))
                                    context.Token = context.Request.Cookies[accessTokenCookieName];

                                return Task.CompletedTask;
                            }
                        };
                    }

                    configureJwtBearer?.Invoke(config);
                });

            return services;
        }
    }
}
