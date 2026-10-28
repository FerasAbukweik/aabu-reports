using AabuReports.Core.Entities;

namespace AabuReports.Core.Abstractions.Repositories;

public interface IRefreshTokenRepository
{
    /// <summary>
    /// add the refresh token to the memory (doesnt save it to DB)
    /// </summary>
    /// <param name="refreshToken"> refresh token to be added </param>
    void AddRefreshToken(RefreshToken refreshToken);

    /// <summary>
    /// remove the refresh token in the memory (doesnt delete it from the DB until changes are saved)
    /// </summary>
    /// <param name="refreshToken"> refresh token to be removed (must be tracked, e.g. from GetByTokenAsync) </param>
    void RemoveRefreshToken(RefreshToken refreshToken);

    /// <summary>
    /// loads all expired refresh tokens and removes them in the memory (doesnt delete them from the DB until changes are saved)
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>returns true if some rows where removed, false if no rows were effected</returns>
    Task<bool> RemoveExpiredRefreshTokensAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// finds a refresh token by its string (the user is included, entity is tracked)
    /// </summary>
    /// <returns> the token, or null if it doesnt exist </returns>
    Task<RefreshToken?> GetByTokenAsync(string refreshTokenString, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// save all changes in memory to DB
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns> True if changes lines are more than 0 </returns>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);
}
