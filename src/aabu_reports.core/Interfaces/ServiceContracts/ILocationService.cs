using aabu_reports.core.Common;
using aabu_reports.core.DTOs.Location;

namespace aabu_reports.core.Interfaces.ServiceContracts;

public interface ILocationService
{
    /// <summary>
    /// gets all locations
    /// </summary>
    Task<Result<IReadOnlyList<LocationDTO>>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// gets a location by its id
    /// </summary>
    /// <returns> result object with the location, or 404 if it doesnt exist </returns>
    Task<Result<LocationDTO>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// creates a new location (it has no reports, so its status is Normal)
    /// </summary>
    /// <returns> result object with the created location </returns>
    Task<Result<LocationDTO>> CreateAsync(LocationCreateDTO locationCreateDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// updates the title, coordinates and type of a location (the status comes from its reports)
    /// </summary>
    /// <returns> result object with the updated location, or 404 if it doesnt exist </returns>
    Task<Result<LocationDTO>> UpdateAsync(LocationUpdateDTO locationUpdateDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// deletes a location
    /// </summary>
    /// <returns> result object, 404 if it doesnt exist </returns>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
