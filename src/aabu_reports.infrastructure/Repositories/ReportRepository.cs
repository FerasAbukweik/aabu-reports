using System.Linq.Expressions;
using aabu_reports.core.DTOs.Lazy;
using aabu_reports.core.DTOs.Report;
using aabu_reports.core.Entities;
using aabu_reports.core.Interfaces.RepositoryContracts;
using Microsoft.EntityFrameworkCore;

namespace aabu_reports.infrastructure.Repositories;

public class ReportRepository(
    ApplicationDbContext dbContext
    ) : IReportRepository
{
    public void AddReport(Report report)
    {
        dbContext.Reports.Add(report);
    }

    public async Task<Report?> RemoveById(Guid id, CancellationToken cancellationToken = default)
    {
        var toRemove = await GetTrackedByIdAsync(id, cancellationToken);
        if (toRemove == null) return null;

        dbContext.Reports.Remove(toRemove);

        return toRemove;
    }

    public async Task<Report?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Reports
            .AsNoTracking()
            .Include(r => r.Location)
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<Report?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Reports.FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyList<Report>> LazyGetAllAsync(LazyDTO lazyDto, CancellationToken cancellationToken = default)
    {
        return await Filter(r => true, [r => r.Location], r => r.ReportDate, true, lazyDto.Skip, lazyDto.Take).ToListAsync(cancellationToken);
    }

    private IQueryable<Report> Filter(
        Expression<Func<Report, bool>> predicate,
        Expression<Func<Report, object?>>[]? include = null,
        Expression<Func<Report, object?>>? orderBy = null,
        bool orderByDesc = false,
        int? skip = null,
        int? take = null
        )
    {
        var query = dbContext.Reports.AsQueryable();

        if (include != null)
        {
            foreach (var inc in include)
            {
                query = query.Include(inc);
            }
        }

        if (orderBy != null)
        {
            if (orderByDesc) query = query.OrderByDescending(orderBy);
            else query = query.OrderBy(orderBy);
        }
        
        if (skip != null) query = query.Skip(skip.Value);
        if (take != null) query = query.Take(take.Value);
        
        return query.Where(predicate);
    }

    public async Task<IReadOnlyList<Report>> FilterAsync(
        Expression<Func<Report, bool>> predicate,
        Expression<Func<Report, object?>>[]? include = null,
        Expression<Func<Report, object?>>? orderBy = null,
        bool orderByDesc = false,
        int? skip = null,
        int? take = null,
        CancellationToken cancellationToken = default)
    {
        return await Filter(predicate, include, orderBy, orderByDesc, skip, take).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Report>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await Filter(r => r.UserId == userId, [r => r.Location]).ToListAsync(cancellationToken);
    }

    public async Task<Report?> UpdateState(ReportUpdateStateDTO reportUpdateStateDto, CancellationToken cancellationToken = default)
    {
        var toUpdate = await GetTrackedByIdAsync(reportUpdateStateDto.Id, cancellationToken);
        if (toUpdate == null) return null;

        toUpdate.ChangeState(reportUpdateStateDto.State);

        return toUpdate;
    }

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken) > 0;
    }
}
