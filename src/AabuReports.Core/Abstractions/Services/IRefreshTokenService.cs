using AabuReports.Core.Results;
using AabuReports.Core.Entities;

namespace AabuReports.Core.Abstractions.Services;

public interface IRefreshTokenService
{
    /// <summary>
    /// generates and store refresh token to the target user (in DB only)
    /// </summary>
    /// <param name="userId"> the target user id </param>
    /// <returns> result object with the added token entity </returns>
    Task<Result<RefreshToken>> GenerateRefreshTokenAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// validates the refresh token and deletes it so it can only be used once (rotation)
    /// </summary>
    /// <returns> result object with the consumed token entity (its User is loaded) </returns>
    Task<Result<RefreshToken>> ConsumeRefreshTokenAsync(string refreshTokenString, CancellationToken cancellationToken = default);

    /// <summary>
    /// deletes the refresh token, succeeds even if it doesnt exist
    /// </summary>
    Task<Result> RevokeRefreshTokenAsync(string refreshTokenString, CancellationToken cancellationToken = default);
}
