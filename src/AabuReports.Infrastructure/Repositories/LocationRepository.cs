using AabuReports.Core.Abstractions.Repositories;
using AabuReports.Core.Dtos.Locations;
using AabuReports.Core.Entities;
using AabuReports.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace AabuReports.Infrastructure.Repositories;

public class LocationRepository(
    ApplicationDbContext dbContext
) : ILocationRepository
{
    public void AddLocation(Location location)
    {
        dbContext.Locations.Add(location);
    }

    public async Task<Location?> RemoveById(Guid id, CancellationToken cancellationToken = default)
    {
        var toRemove = await dbContext.Locations.FindAsync([id], cancellationToken);
        if (toRemove == null) return null;

        dbContext.Locations.Remove(toRemove);

        return toRemove;
    }

    public async Task<Location?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // the highest report state is the biggest number (the enum is ordered by importance),
        // null means the location has no reports
        var row = await dbContext.Locations
            .AsNoTracking()
            .Where(l => l.Id == id)
            .Select(l => new
            {
                Location = l,
                Highest = l.Reports.Max(r => (int?)r.State) ?? 0,
                ActiveReportsCount = l.Reports.Count(r => (int)r.State > (int)ReportStateEnum.Fixed)
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (row == null) return null;

        row.Location.State = (ReportStateEnum)row.Highest;
        row.Location.ActiveReportsCount = row.ActiveReportsCount;

        return row.Location;
    }

    public async Task<IReadOnlyList<Location>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var lastWeekDate = DateTime.UtcNow.AddDays(-7);
        
        var rows = await dbContext.Locations
            .AsNoTracking()
            .Where(l => l.Type == LocationType.Building || (
                l.Reports.Any(r => (int)r.State > (int)ReportStateEnum.Fixed) ||
                l.Reports.Any(r => r.FixDate >= lastWeekDate)
                ))
            .OrderBy(l => l.Title)
            .Select(l => new
            {
                Location = l,
                Highest = l.Reports.Max(r => (int?)r.State) ?? 0,
                ActiveReportsCount = l.Reports.Count(r => (int)r.State > (int)ReportStateEnum.Fixed)
            })
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            row.Location.State = (ReportStateEnum)row.Highest;
            row.Location.ActiveReportsCount = row.ActiveReportsCount;
        }
        
        return rows.Select(r => r.Location).ToList();
    }

    public async Task<Location?> Update(LocationUpdateDto locationUpdateDto,
        CancellationToken cancellationToken = default)
    {
        var toUpdate = await dbContext.Locations.FindAsync([locationUpdateDto.Id], cancellationToken);
        if (toUpdate == null) return null;

        toUpdate.Title = locationUpdateDto.Title ?? toUpdate.Title;
        toUpdate.Coordinates = locationUpdateDto.Coordinates ?? toUpdate.Coordinates;
        toUpdate.Type = locationUpdateDto.Type ?? toUpdate.Type;

        return toUpdate;
    }

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken) > 0;
    }
}