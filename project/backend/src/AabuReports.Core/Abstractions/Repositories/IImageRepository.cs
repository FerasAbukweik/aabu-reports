using System.Linq.Expressions;
using AabuReports.Core.Entities;

namespace AabuReports.Core.Abstractions.Repositories;

public interface IImageRepository
{
    /// <summary>
    /// add the images to the memory (doesnt save them to DB)
    /// </summary>
    /// <param name="images"> images to be added </param>
    void AddRange(IEnumerable<Image> images);

    /// <summary>
    /// loads the images matching the condition with a single query (entities are not tracked)
    /// </summary>
    /// <param name="take"> max number of images to load, null loads all of them </param>
    Task<IReadOnlyList<Image>> FilterAsync(Expression<Func<Image, bool>> where, int? take = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// deletes every image matching the condition directly in the DB with a single query
    /// (it doesnt wait for SaveChangesAsync, and it doesnt touch Cloudinary)
    /// </summary>
    /// <returns> the number of deleted images </returns>
    Task<int> ExecuteRemoveAsync(Expression<Func<Image, bool>> where, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// returns not tracked image
    /// </summary>
    /// <param name="id"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>returns not tracked image</returns>
    Task<Image?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// returns tracked image
    /// </summary>
    /// <param name="id"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>returns tracked image</returns>
    Task<Image?> GetTrackedByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// save all changes in memory to DB
    /// </summary>
    /// <returns> True if changes lines are more than 0 </returns>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);
}
