using aabu_reports.core.Entities;
using aabu_reports.core.Interfaces.RepositoryContracts;
using Microsoft.EntityFrameworkCore;

namespace aabu_reports.infrastructure.Repositories;

public class RefreshTokenRepository(
    ApplicationDbContext dbContext
    ) : IRefreshTokenRepository
{
    public void AddRefreshToken(RefreshToken refreshToken)
    {
        dbContext.RefreshTokens.Add(refreshToken);
    }

    public void RemoveRefreshToken(RefreshToken refreshToken)
    {
        dbContext.RefreshTokens.Remove(refreshToken);
    }

    public async Task RemoveExpiredRefreshTokensAsync(CancellationToken cancellationToken = default)
    {
        var expiredTokens = await dbContext.RefreshTokens
            .Where(rt => rt.ExpiresAt < DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        dbContext.RefreshTokens.RemoveRange(expiredTokens);
    }

    public async Task<RefreshToken?> GetByTokenAsync(string refreshTokenString, CancellationToken cancellationToken = default)
    {
        return await dbContext.RefreshTokens
            .AsNoTracking()
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.RefreshTokenString == refreshTokenString, cancellationToken);
    }

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken) > 0;
    }
}
