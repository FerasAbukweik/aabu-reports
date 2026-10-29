using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Entities;

namespace AabuReports.Core.Abstractions.Repositories;

public interface IMessageRepository
{
    /// <summary>
    /// add the message to the memory (doesnt save it to DB)
    /// </summary>
    /// <param name="message"> message to be added </param>
    void AddMessage(Message message);

    /// <summary>
    /// loads the messages of one report, newest first, with their sender (entities are not tracked)
    /// </summary>
    /// <param name="reportId"> the report whose chat is loaded </param>
    /// <param name="lazyLoadingDto"> skip and take </param>
    /// <param name="cancellationToken"></param>
    Task<IReadOnlyList<Message>> LazyGetByReportIdAsync(Guid reportId, LazyLoadingDto lazyLoadingDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// save all changes in memory to DB
    /// </summary>
    /// <returns> True if changes lines are more than 0 </returns>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);
}
