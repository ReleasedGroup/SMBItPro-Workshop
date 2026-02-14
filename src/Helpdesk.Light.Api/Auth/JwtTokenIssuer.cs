using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Api.Tenancy;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Helpdesk.Light.Api.Auth;

public sealed class JwtTokenIssuer(IOptions<JwtOptions> options) : IJwtTokenIssuer
{
    private readonly JwtOptions jwtOptions = options.Value;

    public string IssueToken(Guid userId, string email, string role, Guid? customerId, out DateTime expiresUtc)
    {
        byte[] keyBytes = Encoding.UTF8.GetBytes(jwtOptions.SigningKey);
        SymmetricSecurityKey key = new(keyBytes);
        SigningCredentials signingCredentials = new(key, SecurityAlgorithms.HmacSha256);

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role)
        ];

        if (customerId.HasValue)
        {
            claims.Add(new Claim(ClaimTypesExtension.CustomerId, customerId.Value.ToString()));
        }

        expiresUtc = DateTime.UtcNow.AddMinutes(jwtOptions.ExpiryMinutes);

        JwtSecurityToken token = new(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            expires: expiresUtc,
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
