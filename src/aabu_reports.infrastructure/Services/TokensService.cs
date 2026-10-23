using aabu_reports.core.Common;
using aabu_reports.core.Constants;
using aabu_reports.core.DTOs.Account;
using aabu_reports.core.DTOs.Auth;
using aabu_reports.core.Entities.Identity;
using aabu_reports.core.Interfaces.ServiceContracts;
using Microsoft.Extensions.Options;

namespace aabu_reports.infrastructure.Services;

public class TokensService(
    IAccessTokenService accessTokenService,
    IRefreshTokenService refreshTokenService,
    IOptions<JwtOptions> jwtOptions) : ITokensService
{
    public async Task<Result<AuthResponseDto>> IssueTokensAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        var accessToken = await accessTokenService.GenerateAccessTokenAsync(user, cancellationToken);
        if (!accessToken.IsSuccess)
            return accessToken.MapFailure<AuthResponseDto>();

        var refreshToken = await refreshTokenService.GenerateRefreshTokenAsync(user.Id, cancellationToken);
        if (!refreshToken.IsSuccess)
            return refreshToken.MapFailure<AuthResponseDto>();

        var refreshTokenEntity = refreshToken.Value!;

        return Result<AuthResponseDto>.Success(new AuthResponseDto
        {
            AccessToken = accessToken.Value!,
            AccessTokenExpiresAt = DateTime.UtcNow.AddSeconds(jwtOptions.Value.AccessTokenLifeTimeInSeconds),
            RefreshToken = refreshTokenEntity.RefreshTokenString,
            RefreshTokenExpiresAt = refreshTokenEntity.ExpiresAt
        });
    }
}
