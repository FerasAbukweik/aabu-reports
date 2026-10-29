using AabuReports.Core.Abstractions.Repositories;
using AabuReports.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace AabuReports.Infrastructure.Repositories;

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

    public async Task<bool> RemoveExpiredRefreshTokensAsync(CancellationToken cancellationToken = default)
    {
        var changedRows = await dbContext.RefreshTokens
            .Where(rt => rt.ExpiresAt < DateTime.UtcNow)
            .ExecuteDeleteAsync(cancellationToken);
        
        return changedRows > 0;
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
