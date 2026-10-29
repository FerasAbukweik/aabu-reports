using System.Linq.Expressions;
using AabuReports.Core.Abstractions.Repositories;
using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Dtos.Reports;
using AabuReports.Core.Entities;
using AabuReports.Core.Enums;
using AabuReports.Core.Extensions;
using AabuReports.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace AabuReports.Infrastructure.Repositories;

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
            .Include(r => r.Images)
            .Include(r => r.Location)
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<Report?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Reports
            .Include(r => r.Images)
            .Include(r => r.Location)
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<Guid?> GetOwnerIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Reports
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => (Guid?)r.UserId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Report>> LazyGetAllAsync(ReportFilterDto filterDto, LazyLoadingDto lazyLoadingDto, CancellationToken cancellationToken = default)
    {
        return await Filter(
            filterDto.ToPredicate(),
            [r => r.Images],
            r => r.ReportDate,
            true,
            lazyLoadingDto.Skip,
            lazyLoadingDto.Take
            ).ToListAsync(cancellationToken);
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
        var query = dbContext.Reports.AsNoTracking().Where(predicate);

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

        return query;
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

    public async Task<Report?> UpdateState(ReportUpdateStateDto reportUpdateStateDto, CancellationToken cancellationToken = default)
    {
        var toUpdate = await GetTrackedByIdAsync(reportUpdateStateDto.Id, cancellationToken);
        if (toUpdate == null) return null;

        toUpdate.ChangeState(reportUpdateStateDto.State);

        return toUpdate;
    }

    public async Task<MaintenanceStatsDto> GetMaintenanceStatsAsync(CancellationToken cancellationToken = default)
    {
        var reports = dbContext.Reports.AsNoTracking();
        var yearStart = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // query 1: every number in one round trip
        var totals = await reports
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Completed = g.Count(r => r.State == ReportStateEnum.Fixed),
                InProgress = g.Count(r => r.State == ReportStateEnum.InProgress),
                YearTotal = g.Count(r => r.ReportDate >= yearStart),
                YearFixed = g.Count(r => r.ReportDate >= yearStart && r.State == ReportStateEnum.Fixed),
                // reports fixed before FixDate existed have no FixDate and are skipped (AVG ignores nulls)
                AverageMinutes = g.Average(r => r.State == ReportStateEnum.Fixed && r.FixDate != null
                    ? (double?)EF.Functions.DateDiffMinute(r.ReportDate, r.FixDate!.Value)
                    : null)
            })
            .SingleOrDefaultAsync(cancellationToken);

        // query 2: the location with the most in-progress reports (the crew location)
        // the location of the latest in-progress report is where the crew is working
        var crewLocation = await reports
            .Where(r => r.State == ReportStateEnum.InProgress)
            .OrderByDescending(r => r.ReportDate)
            .Select(r => r.Location!.Title)
            .FirstOrDefaultAsync(cancellationToken);

        // totals is null when the Reports table is empty (GroupBy over no rows gives no row)
        var yearTotal = totals?.YearTotal ?? 0;
        var yearFixed = totals?.YearFixed ?? 0;

        return new MaintenanceStatsDto
        {
            CompletedReportsCount = totals?.Completed ?? 0,
            CompletionRatePercent = yearTotal == 0 ? 0 : Math.Round(yearFixed * 100.0 / yearTotal, 1),
            InProgressReportsCount = totals?.InProgress ?? 0,
            AverageResponseTimeHours = Math.Round((totals?.AverageMinutes ?? 0) / 60.0, 1),
            ActiveCrewLocation = crewLocation
        };
    }

    public async Task<ReportHistoryStatsDto> GetHistoryStatsAsync(Guid? userId, CancellationToken cancellationToken = default)
    {
        // one query, every number in one round trip
        var totals = await dbContext.Reports
            .AsNoTracking()
            .Where(r => userId == null || r.UserId == userId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                InProgress = g.Count(r => r.State == ReportStateEnum.InProgress),
                Completed = g.Count(r => r.State == ReportStateEnum.Fixed),
                AwaitingRating = g.Count(r => r.State == ReportStateEnum.Fixed && r.Rate == null)
            })
            .SingleOrDefaultAsync(cancellationToken);

        // totals is null when the user has no reports (GroupBy over no rows gives no row)
        return new ReportHistoryStatsDto
        {
            TotalReportsCount = totals?.Total ?? 0,
            InProgressReportsCount = totals?.InProgress ?? 0,
            CompletedReportsCount = totals?.Completed ?? 0,
            AwaitingRatingReportsCount = totals?.AwaitingRating ?? 0
        };
    }

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken) > 0;
    }
}
