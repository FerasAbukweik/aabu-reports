using aabu_reports.core.Constants;
using aabu_reports.core.DTOs.Location;
using aabu_reports.core.Interfaces.ServiceContracts;
using aabu_reports.webapi.ExtensionMethods;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace aabu_reports.webapi.Controllers;

[Authorize]
public class LocationController(ILocationService locationService) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LocationDTO>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await locationService.GetAllAsync(cancellationToken);

        return result.ToActionResult();
    }

    [HttpPut]
    [Authorize(Roles = nameof(Roles.Admin))]
    public async Task<ActionResult<LocationDTO>> Update([FromBody]LocationUpdateDTO locationUpdateDto, CancellationToken cancellationToken)
    {
        var result = await locationService.UpdateAsync(locationUpdateDto, cancellationToken);

        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = nameof(Roles.Admin))]
    public async Task<IActionResult> Delete([FromRoute]Guid id, CancellationToken cancellationToken)
    {
        var result = await locationService.DeleteAsync(id, cancellationToken);

        return result.ToActionResult();
    }
}