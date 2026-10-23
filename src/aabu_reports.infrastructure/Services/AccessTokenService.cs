using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using aabu_reports.core.Common;
using aabu_reports.core.Constants;
using aabu_reports.core.Entities.Identity;
using aabu_reports.core.Interfaces.ServiceContracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace aabu_reports.infrastructure.Services;

public class AccessTokenService(
    ILogger<AccessTokenService> logger,
    UserManager<ApplicationUser> userManager,
    IOptions<JwtOptions> jwtOptions) : IAccessTokenService
{
    public async Task<Result<string>> GenerateAccessTokenAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        // make sure user have username and email
        if (user.UserName == null || user.Email == null)
        {
            logger.LogWarning("{ServiceName}.{MethodName} user with Id: {UserId} failed creating access token because of missing username or email", 
                nameof(AccessTokenService), nameof(GenerateAccessTokenAsync), user.Id.ToString());
            return Result<string>.Failure("user missing username or email");
        }

        // claims
        var claims = new List<Claim>()
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("StudentId", user.StudentId)
        };
        
        // get user roles
        var roles = await userManager.GetRolesAsync(user);
        if (!roles.Any())
        {
            logger.LogWarning("{ServiceName}.{MethodName} user with Id: {UserId} tried creating access token while having no roles",
                nameof(AccessTokenService), nameof(GenerateAccessTokenAsync), user.Id.ToString());
            return Result<string>.Failure("user has no roles");
        }
        
        // add roles to claims
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        
        // make sure we have the key
        if (string.IsNullOrEmpty(jwtOptions.Value.Key))
        {
            logger.LogError("{ServiceName}.{MethodName} failed: JWT signing key is not configured.",
                nameof(AccessTokenService), nameof(GenerateAccessTokenAsync));
            return Result<string>.Failure("Token configuration error.", HttpStatusCode.InternalServerError);
        }
        
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Value.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtOptions.Value.Issuer,
            audience: jwtOptions.Value.Audience,
            claims: claims,
            signingCredentials: creds,
            expires: DateTime.UtcNow.AddSeconds(jwtOptions.Value.AccessTokenLifeTimeInSeconds)
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Result<string>.Success(tokenString);
    }
}