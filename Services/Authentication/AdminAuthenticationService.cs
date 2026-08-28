using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SRS_backend.Configuration;
using SRS_backend.Dtos.Admin.Authentication;
using SRS_backend.Security;

namespace SRS_backend.Services.Authentication;

public sealed class AdminAuthenticationService(
    IOptions<AdminOptions> adminOptions,
    IPasswordHasher<AdminOptions> passwordHasher,
    TimeProvider timeProvider) : IAdminAuthenticationService
{
    private readonly AdminOptions _adminOptions = adminOptions.Value;

    public AdminLoginResponse? Authenticate(AdminLoginRequest request)
    {
        var usernameMatches = CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(request.Username)),
            SHA256.HashData(Encoding.UTF8.GetBytes(_adminOptions.Username)));
        var passwordMatches = passwordHasher.VerifyHashedPassword(
            _adminOptions,
            _adminOptions.PasswordHash,
            request.Password) is not PasswordVerificationResult.Failed;

        if (!(usernameMatches & passwordMatches))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddDays(_adminOptions.TokenLifetimeDays);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_adminOptions.JwtSigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _adminOptions.JwtIssuer,
            audience: _adminOptions.JwtAudience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, _adminOptions.Username),
                new Claim(ClaimTypes.Name, _adminOptions.Username),
                new Claim(ClaimTypes.Role, AuthorizationPolicies.AdminRole),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new AdminLoginResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            "Bearer",
            expiresAt);
    }
}
