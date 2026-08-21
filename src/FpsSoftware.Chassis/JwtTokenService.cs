using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace FpsSoftware.Chassis
{
    public class JwtTokenSettings
    {
        public string Issuer { get; set; } = "";
        public string Audience { get; set; } = "";
        public string SecretKey { get; set; } = "";
        public int TokenExpireSeconds { get; set; }
        public int RefreshTokenExpireSeconds { get; set; }
        public bool ValidateIssuer { get; set; } = true;
        public bool ValidateAudience { get; set; } = true;
        public bool ValidateLifetime { get; set; } = true;
    }

    public static class JwtTokenService
    {
        public static string CreateToken(JwtTokenSettings settings, IReadOnlyCollection<Claim> claims)
            => CreateToken(settings, claims, null);

        public static string CreateToken(JwtTokenSettings settings, IReadOnlyCollection<Claim> claims, DateTime? expires)
        {
            var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey));
            var signInCredentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);

            var tokenOptions = new JwtSecurityToken(
                issuer: settings.Issuer,
                audience: settings.Audience,
                claims: claims,
                expires: expires ?? DateTime.UtcNow.AddSeconds(settings.TokenExpireSeconds),
                signingCredentials: signInCredentials
            );

            return new JwtSecurityTokenHandler().WriteToken(tokenOptions);
        }

        public static ClaimsPrincipal GetPrincipalFromExpiredToken(JwtTokenSettings settings, string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = settings.ValidateAudience && !string.IsNullOrEmpty(settings.Audience),
                ValidAudience = settings.Audience,
                ValidateIssuer = settings.ValidateIssuer && !string.IsNullOrEmpty(settings.Issuer),
                ValidIssuer = settings.Issuer,
                ValidateLifetime = false,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey))
            };

            var principal = new JwtSecurityTokenHandler().ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);
            if (!(securityToken is JwtSecurityToken jwtSecurityToken) || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                throw new SecurityTokenException("GetPrincipalFromExpiredToken Token is not validated");

            return principal;
        }
    }
}
