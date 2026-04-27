using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CentralAuthNotificationPlatform.Dtos;
using CentralAuthNotificationPlatform.Models;
using CentralAuthNotificationPlatform.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CentralAuthNotificationPlatform.Services;

public interface IJwtTokenService
{
    AuthResponse CreatePlatformToken(ApplicationUser user, IReadOnlyCollection<string> roles);
    string CreateAccessToken(ApplicationUser user, string audience, string scope, int expirationMinutes);
    string CreateIdToken(ApplicationUser user, string audience, string? nonce, int expirationMinutes);
}

public sealed class JwtTokenService(IOptions<JwtOptions> jwtOptions) : IJwtTokenService
{
    private readonly JwtOptions _options = jwtOptions.Value;

    public AuthResponse CreatePlatformToken(ApplicationUser user, IReadOnlyCollection<string> roles)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(Math.Max(5, _options.ExpirationMinutes));
        var token = CreateJwt(
            user,
            _options.Audience,
            expiresAt,
            [
                new Claim("scope", "platform_api"),
                .. roles.Select(role => new Claim(ClaimTypes.Role, role))
            ]);

        return new AuthResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.DisplayName,
            roles.ToArray(),
            token,
            expiresAt);
    }

    public string CreateAccessToken(ApplicationUser user, string audience, string scope, int expirationMinutes)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(expirationMinutes, 5, 60));
        return CreateJwt(
            user,
            audience,
            expiresAt,
            [
                new Claim("scope", scope)
            ]);
    }

    public string CreateIdToken(ApplicationUser user, string audience, string? nonce, int expirationMinutes)
    {
        var claims = new List<Claim>
        {
            new("name", user.DisplayName)
        };

        if (!string.IsNullOrWhiteSpace(nonce))
        {
            claims.Add(new Claim("nonce", nonce));
        }

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(Math.Clamp(expirationMinutes, 5, 60));
        return CreateJwt(user, audience, expiresAt, claims);
    }

    private string CreateJwt(ApplicationUser user, string audience, DateTimeOffset expiresAt, IReadOnlyCollection<Claim> additionalClaims)
    {
        if (string.IsNullOrWhiteSpace(_options.SigningKey) || _options.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("JWT signing key must be at least 32 characters.");
        }

        var now = DateTimeOffset.UtcNow;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.DisplayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(additionalClaims);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
