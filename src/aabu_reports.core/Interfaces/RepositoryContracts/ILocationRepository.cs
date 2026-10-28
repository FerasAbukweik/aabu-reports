using aabu_reports.core.DTOs.Location;
using aabu_reports.core.Entities;

namespace aabu_reports.core.Interfaces.RepositoryContracts;

public interface ILocationRepository
{
    /// <summary>
    /// add the location to the memory (doesnt save it to DB)
    /// </summary>
    /// <param name="location"> location to be added </param>
    void AddLocation(Location location);

    /// <summary>
    /// remove the location in the memory (doesnt delete it from the DB until changes are saved)
    /// </summary>
    /// <param name="id">id for the location to be removed</param>
    /// <param name="cancellationToken"></param>
    /// <returns> returns the removed location -- returns null if no location was found</returns>
    Task<Location?> RemoveById(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// finds a location by its id (entity is not tracked), Status is the highest state among its reports (Normal if none)
    /// </summary>
    /// <returns> the location, or null if it doesnt exist </returns>
    Task<Location?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// loads all locations (entities are not tracked), Status is the highest state among their reports (Normal if none)
    /// </summary>
    Task<IReadOnlyList<Location>> GetAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// update location
    /// </summary>
    /// <param name="locationUpdateDto">new location data</param>
    /// <param name="cancellationToken"></param>
    /// <returns>returns the updated location</returns>
    Task<Location?> Update(LocationUpdateDTO locationUpdateDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// save all changes in memory to DB
    /// </summary>
    /// <returns> True if changes lines are more than 0 </returns>
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);
}
