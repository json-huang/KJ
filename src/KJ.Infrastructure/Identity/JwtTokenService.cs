using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KJ.Domain.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace KJ.Infrastructure.Identity;

public sealed class JwtTokenService : ITokenService
{
    private static readonly ConcurrentDictionary<string, DateTimeOffset> Revoked = new();

    private readonly JwtOptions _options;
    private readonly JwtSecurityTokenHandler _handler = new();

    public JwtTokenService(IConfiguration configuration)
    {
        _options = new JwtOptions();
        configuration.GetSection("Identity:Jwt").Bind(_options);
    }

    public Task<TokenResult> GenerateTokenAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Secret))
            throw new InvalidOperationException("Identity:Jwt:Secret is missing.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTimeOffset.UtcNow.AddMinutes(_options.ExpirationMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        var tokenString = _handler.WriteToken(token);
        return Task.FromResult(new TokenResult(tokenString, expires));
    }

    public Task<KJ.Domain.Identity.TokenValidationResult> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Task.FromResult(new KJ.Domain.Identity.TokenValidationResult(false, null, "Token is empty."));

        if (Revoked.ContainsKey(token))
            return Task.FromResult(new KJ.Domain.Identity.TokenValidationResult(false, null, "Token revoked."));

        if (string.IsNullOrWhiteSpace(_options.Secret))
            return Task.FromResult(new KJ.Domain.Identity.TokenValidationResult(false, null, "Identity:Jwt:Secret is missing."));

        try
        {
            var parameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret)),
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = true,
                ValidAudience = _options.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2),
            };

            var principal = _handler.ValidateToken(token, parameters, out _);
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            return Task.FromResult(new KJ.Domain.Identity.TokenValidationResult(true, userId, null));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new KJ.Domain.Identity.TokenValidationResult(false, null, ex.Message));
        }
    }

    public Task RevokeTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(token))
            Revoked[token] = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<bool> IsTokenRevokedAsync(string token, CancellationToken cancellationToken = default) =>
        Task.FromResult(!string.IsNullOrWhiteSpace(token) && Revoked.ContainsKey(token));
}

