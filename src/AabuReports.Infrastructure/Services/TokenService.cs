using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.Auth;
using AabuReports.Core.Entities.Identity;
using AabuReports.Core.Results;

namespace AabuReports.Infrastructure.Services;

public class TokenService(
    IAccessTokenService accessTokenService,
    IRefreshTokenService refreshTokenService) : ITokenService
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
            AccessToken = accessToken.Value!.AccessToken,
            AccessTokenExpiresAt = accessToken.Value!.ExpiresAt,
            RefreshToken = refreshTokenEntity.RefreshTokenString,
            RefreshTokenExpiresAt = refreshTokenEntity.ExpiresAt
        });
    }
}
