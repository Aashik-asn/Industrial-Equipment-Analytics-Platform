using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CIIP.Backend.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CIIP.Backend.Services;

public class JwtService
{
    private readonly IConfiguration _config;

    public JwtService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateToken(UserAccount user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

        var creds = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim("tenantId", user.TenantId.ToString()),
            new Claim(ClaimTypes.Role, user.Role ?? "USER"),
            new Claim(ClaimTypes.Email, user.Email ?? "")
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                Convert.ToInt32(_config["Jwt:ExpireMinutes"])
            ),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
