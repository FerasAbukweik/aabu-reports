using AabuReports.Core.Results;
using AabuReports.Core.Dtos.Locations;

namespace AabuReports.Core.Abstractions.Services;

public interface ILocationService
{
    /// <summary>
    /// gets all locations
    /// </summary>
    Task<Result<IReadOnlyList<LocationDto>>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// gets a location by its id
    /// </summary>
    /// <returns> result object with the location, or 404 if it doesnt exist </returns>
    Task<Result<LocationDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// creates a new location (it has no reports, so its status is Normal)
    /// </summary>
    /// <returns> result object with the created location </returns>
    Task<Result<LocationDto>> CreateAsync(LocationCreateDto locationCreateDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// updates the title, coordinates and type of a location (the status comes from its reports)
    /// </summary>
    /// <returns> result object with the updated location, or 404 if it doesnt exist </returns>
    Task<Result<LocationDto>> UpdateAsync(LocationUpdateDto locationUpdateDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// deletes a location
    /// </summary>
    /// <returns> result object, 404 if it doesnt exist </returns>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
