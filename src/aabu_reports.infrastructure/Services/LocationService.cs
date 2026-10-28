using System.Net;
using aabu_reports.core.Classes;
using aabu_reports.core.Common;
using aabu_reports.core.DTOs.Location;
using aabu_reports.core.Entities;
using aabu_reports.core.Interfaces.RepositoryContracts;
using aabu_reports.core.Interfaces.ServiceContracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace aabu_reports.infrastructure.Services;

public class LocationService(
    ILocationRepository locationRepository,
    ILogger<LocationService> logger) : ILocationService
{
    private const string NotFoundMessage = "Location not found";

    public async Task<Result<IReadOnlyList<LocationDTO>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var locations = await locationRepository.GetAllAsync(cancellationToken);

        return Result<IReadOnlyList<LocationDTO>>.Success(locations.Select(l => l.ToDTO()).ToList());
    }

    public async Task<Result<LocationDTO>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var location = await locationRepository.GetByIdAsync(id, cancellationToken);
        if (location is null)
            return Result<LocationDTO>.Failure(NotFoundMessage, HttpStatusCode.NotFound);

        return Result<LocationDTO>.Success(location.ToDTO());
    }

    public async Task<Result<LocationDTO>> CreateAsync(LocationCreateDTO locationCreateDto, CancellationToken cancellationToken = default)
    {
        var newLocation = new Location
        {
            Id = Guid.NewGuid(),
            Title = locationCreateDto.Title.Trim(),
            Coordinates = new MapCoordinates
            {
                Lat = locationCreateDto.Coordinates.Lat,
                Lng = locationCreateDto.Coordinates.Lng
            },
            Type = locationCreateDto.Type
        };

        locationRepository.AddLocation(newLocation);

        if (!await locationRepository.SaveChangesAsync(cancellationToken))
        {
            var errorMessage = "no changes where applied to the DB";
            logger.LogError("{ServiceName}.{MethodName} " + errorMessage,
                nameof(LocationService), nameof(CreateAsync));

            return Result<LocationDTO>.Failure(errorMessage, HttpStatusCode.InternalServerError);
        }

        return Result<LocationDTO>.Success(newLocation.ToDTO(), HttpStatusCode.Created);
    }

    public async Task<Result<LocationDTO>> UpdateAsync(LocationUpdateDTO locationUpdateDto, CancellationToken cancellationToken = default)
    {
        var updated = await locationRepository.Update(locationUpdateDto, cancellationToken);
        if(updated == null) return  Result<LocationDTO>.Failure(NotFoundMessage, HttpStatusCode.NotFound);

        if (!await locationRepository.SaveChangesAsync(cancellationToken))
        {
            var errorMessage = "no changes where applied to the DB";
            logger.LogError("{ServiceName}.{MethodName} " + errorMessage,
                nameof(LocationService), nameof(UpdateAsync));

            return Result<LocationDTO>.Failure(errorMessage, HttpStatusCode.InternalServerError);
        }

        return Result<LocationDTO>.Success(updated.ToDTO());
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var removed = await locationRepository.RemoveById(id, cancellationToken);
        if (removed is null)
            return Result.Failure(NotFoundMessage, HttpStatusCode.NotFound);

        if (!await locationRepository.SaveChangesAsync(cancellationToken))
        {
            var errorMessage = "no changes where applied to the DB";
            logger.LogError("{ServiceName}.{MethodName} " + errorMessage,
                nameof(LocationService), nameof(DeleteAsync));

            return Result<LocationDTO>.Failure(errorMessage, HttpStatusCode.InternalServerError);
        }

        return Result.Success(HttpStatusCode.NoContent);
    }
}
