using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.Locations;
using AabuReports.Core.Enums;
using AabuReports.Webapi.ExtensionMethods;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AabuReports.Webapi.Controllers;

[Authorize]
public class LocationController(ILocationService locationService) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LocationDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await locationService.GetAllAsync(cancellationToken);

        return result.ToActionResult();
    }

    [HttpPut]
    [Authorize(Roles = nameof(AppRole.Admin))]
    public async Task<ActionResult<LocationDto>> Update([FromBody]LocationUpdateDto locationUpdateDto, CancellationToken cancellationToken)
    {
        var result = await locationService.UpdateAsync(locationUpdateDto, cancellationToken);

        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = nameof(AppRole.Admin))]
    public async Task<IActionResult> Delete([FromRoute]Guid id, CancellationToken cancellationToken)
    {
        var result = await locationService.DeleteAsync(id, cancellationToken);

        return result.ToActionResult();
    }
}