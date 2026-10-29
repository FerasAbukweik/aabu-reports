using System.Linq.Expressions;
using AabuReports.Core.Abstractions.Repositories;
using AabuReports.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace AabuReports.Infrastructure.Repositories;

public class ImageRepository(
    ApplicationDbContext dbContext
) : IImageRepository
{
    public void AddRange(IEnumerable<Image> images)
    {
        dbContext.Images.AddRange(images);
    }

    public async Task<IReadOnlyList<Image>> FilterAsync(Expression<Func<Image, bool>> where, int? take = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Images
            .AsNoTracking()
            .Where(where);

        if (take != null) query = query.Take(take.Value);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<int> ExecuteRemoveAsync(Expression<Func<Image, bool>> where, CancellationToken cancellationToken = default)
    {
        return await dbContext.Images
            .Where(where)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<Image?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Images.SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<Image?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Images.FindAsync([id], cancellationToken);
    }

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken) > 0;
    }
}
