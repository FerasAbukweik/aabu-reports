using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Dtos.Reports;
using AabuReports.Core.Enums;
using AabuReports.Webapi.ExtensionMethods;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AabuReports.Webapi.Controllers;

[Authorize]
public class ReportController(IReportService reportService) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Roles = nameof(AppRole.Admin))]
    public async Task<ActionResult<IReadOnlyList<ReportDto>>> GetAll([FromQuery] LazyLoadingDto lazyLoadingDto, CancellationToken cancellationToken)
    {
        var result = await reportService.LazyGetAllAsync(lazyLoadingDto, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<ReportDto>>> GetMine(CancellationToken cancellationToken)
    {
        var result = await reportService.GetByUserIdAsync(User.GetUserId(), cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("location")]
    public async Task<ActionResult<IReadOnlyList<ReportDto>>> LazyGetLocationReportsAsync(
        [FromQuery] Guid locationId,
        [FromQuery] LazyLoadingDto lazyLoadingDto, CancellationToken cancellationToken)
    {
        var result = await reportService.LazyGetLocationReportsAsync(locationId, lazyLoadingDto, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReportDto>> GetById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await reportService.GetByIdAsync(id, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<ActionResult<ReportCreateResponseDto>> CreateForLocation([FromBody] ReportCreateForLocationDto reportCreateForLocationDto, CancellationToken cancellationToken)
    {
        var result = await reportService.CreateForLocationIdAsync(User.GetUserId(), reportCreateForLocationDto, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost("coordinates")]
    public async Task<ActionResult<ReportCreateResponseDto>> CreateForCoordinates([FromBody] ReportCreateForCoordinatesDto reportCreateForCoordinatesDto, CancellationToken cancellationToken)
    {
        var result = await reportService.CreateForCoordinates(User.GetUserId(), reportCreateForCoordinatesDto, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPut("state")]
    [Authorize(Roles = nameof(AppRole.Admin))]
    public async Task<ActionResult<ReportDto>> UpdateState([FromBody] ReportUpdateStateDto reportUpdateStateDto, CancellationToken cancellationToken)
    {
        var result = await reportService.UpdateStateAsync(reportUpdateStateDto, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPut("rate")]
    public async Task<ActionResult<ReportDto>> Rate([FromBody] ReportRateDto reportRateDto, CancellationToken cancellationToken)
    {
        var result = await reportService.RateAsync(User.GetUserId(), reportRateDto, cancellationToken);

        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = nameof(AppRole.Admin))]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await reportService.DeleteAsync(id, cancellationToken);

        return result.ToActionResult();
    }
}