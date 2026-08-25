using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using NextChats.Core.Configuration;

namespace NextChats.Api.Security;

/// <summary>JWT 签发（uid / role / name 声明）</summary>
public static class JwtTokenFactory
{
    public static byte[] GetKey(SecurityOptions options)
    {
        var raw = options.JwtKey.Length >= 32 ? options.JwtKey : options.JwtKey.PadRight(32, 'x');
        return Encoding.UTF8.GetBytes(raw[..32]);
    }

    public static string Issue(SecurityOptions options, Guid userId, string username, string displayName, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new("uid", userId.ToString()),
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new("name", displayName),
        };
        foreach (var role in roles.Distinct())
        {
            claims.Add(new Claim("role", role));
        }

        var now = DateTimeOffset.UtcNow;
        var token = new JwtSecurityToken(
            issuer: options.JwtIssuer,
            audience: options.JwtAudience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: now.AddMinutes(options.JwtExpireMinutes).UtcDateTime,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(GetKey(options)), SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
