using System.Net;
using System.Security.Cryptography;
using AabuReports.Core.Abstractions.Repositories;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Entities;
using AabuReports.Core.Options;
using AabuReports.Core.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AabuReports.Infrastructure.Services;

public class RefreshTokenService(
    IRefreshTokenRepository refreshTokenRepository,
    ILogger<RefreshTokenService> logger,
    IOptions<JwtOptions> jwtOptions) : IRefreshTokenService
{
    public async Task<Result<RefreshToken>> GenerateRefreshTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // the refresh token to be added
        var newRefreshToken = new RefreshToken()
        {
            RefreshTokenString = GenerateRefreshTokenString(),
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddSeconds(jwtOptions.Value.RefreshTokenLifeTimeInSeconds)
        };
        
        // add the refresh token to the DB
        refreshTokenRepository.AddRefreshToken(newRefreshToken);
        
        // save changes to DB
        if (!await refreshTokenRepository.SaveChangesAsync(cancellationToken))
        {
            var errorMessage = "no changes where applied to the DB";
            logger.LogError("{ServiceName}.{MethodName} " + errorMessage,
                nameof(RefreshTokenService), nameof(GenerateRefreshTokenAsync));

            return Result<RefreshToken>.Failure(errorMessage, HttpStatusCode.InternalServerError);
        }

        return Result<RefreshToken>.Success(newRefreshToken);
    }

    public async Task<Result<RefreshToken>> ConsumeRefreshTokenAsync(string refreshTokenString, CancellationToken cancellationToken = default)
    {
        const string invalidMessage = "Invalid refresh token";

        var token = await refreshTokenRepository.GetByTokenAsync(refreshTokenString, cancellationToken);
        if (token is null)
            return Result<RefreshToken>.Failure(invalidMessage, HttpStatusCode.Unauthorized);

        // delete it (a used or expired token is never valid again)
        refreshTokenRepository.RemoveRefreshToken(token);

        try
        {
            await refreshTokenRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // the row was already deleted, so another request used this token first
            return Result<RefreshToken>.Failure(invalidMessage, HttpStatusCode.Unauthorized);
        }

        if (token.IsExpired)
            return Result<RefreshToken>.Failure("Refresh token expired", HttpStatusCode.Unauthorized);

        return Result<RefreshToken>.Success(token);
    }

    public async Task<Result> RevokeRefreshTokenAsync(string refreshTokenString, CancellationToken cancellationToken = default)
    {
        var token = await refreshTokenRepository.GetByTokenAsync(refreshTokenString, cancellationToken);
        if (token is null)
            return Result.Success(HttpStatusCode.NoContent);

        refreshTokenRepository.RemoveRefreshToken(token);

        try
        {
            await refreshTokenRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // already deleted by another request, which is what we wanted anyway
        }

        return Result.Success(HttpStatusCode.NoContent);
    }
    
    
    // private
    
    /// <summary>
    /// generates random refresh token string
    /// </summary>
    /// <returns> refresh token string </returns>
    private string GenerateRefreshTokenString()
    {
        byte[] bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        return Convert.ToBase64String(bytes);
    }
}
