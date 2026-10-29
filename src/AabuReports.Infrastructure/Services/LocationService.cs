using System.Net;
using AabuReports.Core.Abstractions.Repositories;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.Locations;
using AabuReports.Core.Entities;
using AabuReports.Core.Enums;
using AabuReports.Core.Results;
using AabuReports.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace AabuReports.Infrastructure.Services;

public class LocationService(
    ILocationRepository locationRepository,
    ILogger<LocationService> logger) : ILocationService
{
    private const string NotFoundMessage = "Location not found";

    public async Task<Result<IReadOnlyList<LocationDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var locations = await locationRepository.GetAllAsync(cancellationToken);

        return Result<IReadOnlyList<LocationDto>>.Success(locations.Select(l => l.ToDto()).ToList());
    }

    public async Task<Result<LocationDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var location = await locationRepository.GetByIdAsync(id, cancellationToken);
        if (location is null)
            return Result<LocationDto>.Failure(NotFoundMessage, HttpStatusCode.NotFound);

        return Result<LocationDto>.Success(location.ToDto());
    }

    public async Task<Result<LocationDto>> CreateAsync(LocationCreateDto locationCreateDto, CancellationToken cancellationToken = default)
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

            return Result<LocationDto>.Failure(errorMessage, HttpStatusCode.InternalServerError);
        }

        if (newLocation.Type == LocationType.Building)
        {
            newLocation.State = ReportStateEnum.Normal;
            newLocation.ReportsCount = 0;
        }
        return Result<LocationDto>.Success(newLocation.ToDto(), HttpStatusCode.Created);
    }

    public async Task<Result<LocationDto>> UpdateAsync(LocationUpdateDto locationUpdateDto, CancellationToken cancellationToken = default)
    {
        var updated = await locationRepository.Update(locationUpdateDto, cancellationToken);
        if(updated == null) return  Result<LocationDto>.Failure(NotFoundMessage, HttpStatusCode.NotFound);

        if (!await locationRepository.SaveChangesAsync(cancellationToken))
        {
            var errorMessage = "no changes where applied to the DB";
            logger.LogError("{ServiceName}.{MethodName} " + errorMessage,
                nameof(LocationService), nameof(UpdateAsync));

            return Result<LocationDto>.Failure(errorMessage, HttpStatusCode.InternalServerError);
        }

        return Result<LocationDto>.Success(updated.ToDto());
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

            return Result<LocationDto>.Failure(errorMessage, HttpStatusCode.InternalServerError);
        }

        return Result.Success(HttpStatusCode.NoContent);
    }
}
