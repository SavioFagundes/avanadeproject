using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SalesService.Auth;

namespace SalesService.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/login", ([FromBody] LoginDto dto, IConfiguration cfg) =>
        {
            if (dto.Username == "demo" && dto.Password == "demo")
            {
                var settings = new JwtSettings();
                cfg.GetSection("JWT").Bind(settings);
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Secret));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
                var token = new JwtSecurityToken(
                    issuer: settings.Issuer,
                    audience: settings.Audience,
                    claims: new[] { new Claim(ClaimTypes.Name, "demo") },
                    expires: DateTime.UtcNow.AddHours(6),
                    signingCredentials: creds
                );
                return Results.Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
            }
            return Results.Unauthorized();
        }).AllowAnonymous();
        return app;
    }
}

public record LoginDto(string Username, string Password);
