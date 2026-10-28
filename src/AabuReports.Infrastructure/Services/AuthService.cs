using System.Net;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.Auth;
using AabuReports.Core.Entities.Identity;
using AabuReports.Core.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace AabuReports.Infrastructure.Services;

public class AuthService(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    IRefreshTokenService refreshTokenService,
    ICookieService cookieService,
    ILogger<AuthService> logger) : IAuthService
{
    private const string LockedOutMessage = "Account is temporarily locked, try again later";

    public async Task<Result> Login(LoginDto loginDto, CancellationToken cancellationToken = default)
    {
        string invalidCredentialsMessage = "Invalid student id or password";

        var user = await userManager.FindByNameAsync(loginDto.StudentId.Trim());
        if (user is null)
            return Result.Failure(invalidCredentialsMessage, HttpStatusCode.Unauthorized);

        if (await userManager.IsLockedOutAsync(user))
        {
            logger.LogWarning("{ServiceName}.{MethodName} login attempt for locked out user with Id: {UserId}",
                nameof(AuthService), nameof(Login), user.Id.ToString());
            return Result.Failure(LockedOutMessage, HttpStatusCode.TooManyRequests);
        }

        if (!await userManager.CheckPasswordAsync(user, loginDto.Password))
        {
            await userManager.AccessFailedAsync(user);
            logger.LogWarning("{ServiceName}.{MethodName} wrong password for user with Id: {UserId}",
                nameof(AuthService), nameof(Login), user.Id.ToString());
            return Result.Failure(invalidCredentialsMessage, HttpStatusCode.Unauthorized);
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var tokensResult = await tokenService.IssueTokensAsync(user, cancellationToken);
        if (!tokensResult.IsSuccess)
        {
            logger.LogError("{ServiceName}.{MethodName} failed issuing tokens for user with Id: {UserId}. Error: {Error}",
                nameof(AuthService), nameof(Login), user.Id.ToString(), tokensResult.ErrorMessage);
            return tokensResult;
        }

        cookieService.SetAuthCookies(tokensResult.Value!);

        return Result.Success();
    }

    /// <remarks>
    /// the endpoint calling this must use [Transactional], so the old token is only deleted if the new one is saved
    /// </remarks>
    public async Task<Result> Refresh(CancellationToken cancellationToken = default)
    {
        var refreshToken = cookieService.GetRefreshToken();
        if (string.IsNullOrEmpty(refreshToken))
            return Result.Failure("Refresh token is missing", HttpStatusCode.Unauthorized);

        var result = await RefreshInternalAsync(refreshToken, cancellationToken);
        if (!result.IsSuccess)
        {
            logger.LogWarning("{ServiceName}.{MethodName} refresh failed. Error: {Error}",
                nameof(AuthService), nameof(Refresh), result.ErrorMessage);

            // the cookie is useless now, dont keep sending it
            cookieService.ClearAuthCookies();
            return result;
        }

        cookieService.SetAuthCookies(result.Value!);

        return Result.Success();
    }

    public async Task<Result> Logout(CancellationToken cancellationToken = default)
    {
        var refreshToken = cookieService.GetRefreshToken();

        // revoke the token in the DB so it cant be used again, even if someone copied the cookie
        if (!string.IsNullOrEmpty(refreshToken))
            await refreshTokenService.RevokeRefreshTokenAsync(refreshToken, cancellationToken);

        // always remove the cookies from the user, whatever happened above
        cookieService.ClearAuthCookies();

        return Result.Success(HttpStatusCode.NoContent);
    }

    // private

    private async Task<Result<AuthResponseDto>> RefreshInternalAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var consumed = await refreshTokenService.ConsumeRefreshTokenAsync(refreshToken, cancellationToken);
        if (!consumed.IsSuccess)
            return consumed.MapFailure<AuthResponseDto>();

        var user = consumed.Value?.User;
        if (user is null)
            return Result<AuthResponseDto>.Failure("Invalid refresh token", HttpStatusCode.Unauthorized);

        if (await userManager.IsLockedOutAsync(user))
            return Result<AuthResponseDto>.Failure(LockedOutMessage, HttpStatusCode.TooManyRequests);

        return await tokenService.IssueTokensAsync(user, cancellationToken);
    }
}
