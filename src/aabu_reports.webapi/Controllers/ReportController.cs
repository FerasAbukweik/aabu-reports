using aabu_reports.core.Constants;
using aabu_reports.core.DTOs.Lazy;
using aabu_reports.core.DTOs.Report;
using aabu_reports.core.Interfaces.ServiceContracts;
using aabu_reports.webapi.ExtensionMethods;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace aabu_reports.webapi.Controllers;

[Authorize]
public class ReportController(IReportService reportService) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Roles = nameof(Roles.Admin))]
    public async Task<ActionResult<IReadOnlyList<ReportDTO>>> GetAll([FromQuery] LazyDTO lazyDto, CancellationToken cancellationToken)
    {
        var result = await reportService.LazyGetAllAsync(lazyDto, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<ReportDTO>>> GetMine(CancellationToken cancellationToken)
    {
        var result = await reportService.GetByUserIdAsync(User.GetUserId(), cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("location")]
    public async Task<ActionResult<IReadOnlyList<ReportDTO>>> LazyGetLocationReportsAsync(
        [FromQuery] Guid locationId,
        [FromQuery] LazyDTO lazyDto, CancellationToken cancellationToken)
    {
        var result = await reportService.LazyGetLocationReportsAsync(locationId, lazyDto, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReportDTO>> GetById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await reportService.GetByIdAsync(id, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<ActionResult<ReportCreateResponseDTO>> CreateForLocation([FromBody] ReportCreateForLocationDTO reportCreateForLocationDto, CancellationToken cancellationToken)
    {
        var result = await reportService.CreateForLocationIdAsync(User.GetUserId(), reportCreateForLocationDto, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost("coordinates")]
    public async Task<ActionResult<ReportCreateResponseDTO>> CreateForCoordinates([FromBody] ReportCreateForCoordinatesDTO reportCreateForCoordinatesDto, CancellationToken cancellationToken)
    {
        var result = await reportService.CreateForCoordinates(User.GetUserId(), reportCreateForCoordinatesDto, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPut("state")]
    [Authorize(Roles = nameof(Roles.Admin))]
    public async Task<ActionResult<ReportDTO>> UpdateState([FromBody] ReportUpdateStateDTO reportUpdateStateDto, CancellationToken cancellationToken)
    {
        var result = await reportService.UpdateStateAsync(reportUpdateStateDto, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPut("rate")]
    public async Task<ActionResult<ReportDTO>> Rate([FromBody] ReportRateDTO reportRateDto, CancellationToken cancellationToken)
    {
        var result = await reportService.RateAsync(User.GetUserId(), reportRateDto, cancellationToken);

        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = nameof(Roles.Admin))]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await reportService.DeleteAsync(id, cancellationToken);

        return result.ToActionResult();
    }
}