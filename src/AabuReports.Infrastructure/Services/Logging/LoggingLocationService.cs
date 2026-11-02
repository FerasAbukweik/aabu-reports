using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Results;
using AabuReports.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AabuReports.Core.Dtos.Locations;

namespace AabuReports.Infrastructure.Services.Logging;

/// <summary> logs the failed results of <see cref="LocationService"/> </summary>
public class LoggingLocationService(
    [FromKeyedServices("core")] ILocationService innerService,
    ILogger<LocationService> logger) : ILocationService
{
    private const string Name = nameof(LocationService);

    public Task<Result<IReadOnlyList<LocationDto>>> GetAllAsync(CancellationToken cancellationToken = default)
        => innerService.GetAllAsync(cancellationToken).LogFailure(logger, Name);

    public Task<Result<LocationDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => innerService.GetByIdAsync(id, cancellationToken).LogFailure(logger, Name);

    public Task<Result<LocationDto>> CreateAsync(LocationCreateDto locationCreateDto, CancellationToken cancellationToken = default)
        => innerService.CreateAsync(locationCreateDto, cancellationToken).LogFailure(logger, Name);

    public Task<Result<LocationDto>> UpdateAsync(LocationUpdateDto locationUpdateDto, CancellationToken cancellationToken = default)
        => innerService.UpdateAsync(locationUpdateDto, cancellationToken).LogFailure(logger, Name);

    public Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => innerService.DeleteAsync(id, cancellationToken).LogFailure(logger, Name);
}
