using AabuReports.Core.Abstractions.Repositories;
using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace AabuReports.Infrastructure.Repositories;

public class MessageRepository(
    ApplicationDbContext dbContext
    ) : IMessageRepository
{
    public void AddMessage(Message message)
    {
        dbContext.Messages.Add(message);
    }

    public async Task<IReadOnlyList<Message>> LazyGetByReportIdAsync(Guid reportId, LazyLoadingDto lazyLoadingDto, CancellationToken cancellationToken = default)
    {
        return await dbContext.Messages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Where(m => m.ReportId == reportId)
            .OrderByDescending(m => m.SentAt)
            .Skip(lazyLoadingDto.Skip)
            .Take(lazyLoadingDto.Take)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken) > 0;
    }
}
