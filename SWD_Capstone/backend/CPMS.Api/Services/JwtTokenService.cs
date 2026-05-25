using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CPMS.Core.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CPMS.Api.Services;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string Key { get; init; }
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 7;
}

public sealed class JwtTokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    public string CreateAccessToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (string RawToken, RefreshToken Entity) CreateRefreshToken(User user, string? deviceInfo)
    {
        ArgumentNullException.ThrowIfNull(user);

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return (rawToken, new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashRefreshToken(rawToken),
            ExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDays),
            DeviceInfo = deviceInfo
        });
    }

    public static string HashRefreshToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
