using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CPMS.BLL.Accounts;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CPMS.API.Services;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string Key { get; init; }
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 7;
}

public sealed class JwtTokenService(IOptions<JwtOptions> options) : IAuthenticationTokenIssuer
{
    private readonly JwtOptions _options = options.Value;

    public IssuedToken Issue(AuthIdentity identity, string? deviceInfo)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, identity.Id.ToString(CultureInfo.InvariantCulture)),
            new Claim("sub", identity.Id.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Name, identity.Username),
            new Claim("name", identity.Username),
            new Claim(ClaimTypes.Role, identity.Role.ToString()),
            new Claim("role", identity.Role.ToString())
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes),
            signingCredentials: credentials);

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return new IssuedToken(
            new JwtSecurityTokenHandler().WriteToken(token), rawToken,
            HashRefreshToken(rawToken), DateTime.UtcNow.AddDays(_options.RefreshTokenDays));
    }

    public string HashRefreshToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
