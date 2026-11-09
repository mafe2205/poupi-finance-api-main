using AppFinancesClassLibrary.Application.Login.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AppFinancesClassLibrary.Infrastructure.Security
{
    public class JwtTokenService(IConfiguration configuration) : ITokenService
    {
        private readonly IConfiguration configuration = configuration;

        public (string Token, DateTime ExpiresAt) GenerateToken(long userId, string role)
        {
            var key = configuration["Jwt:Key"]!;
            var issuer = configuration["Jwt:Issuer"];
            var audience = configuration["Jwt:Audience"];
            var expirationMinutes = int.TryParse(configuration["Jwt:ExpirationMinutes"], out var minutes) ? minutes : 60;

            var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

            var claims = new List<Claim>
            {
                new("perfil_id", userId.ToString()),
                new("papel", role),
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Role, role)
            };

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
        }
    }
}
