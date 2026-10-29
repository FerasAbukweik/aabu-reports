using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Dtos.Reports;
using AabuReports.Core.Enums;
using AabuReports.Core.Results;
using AabuReports.Webapi.ExtensionMethods;
using AabuReports.Webapi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AabuReports.Webapi.Controllers;

[Authorize]
public class ReportController(IReportService reportService) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Roles = nameof(AppRole.Admin))]
    public async Task<ActionResult<IReadOnlyList<ReportDto>>> LazyGetAll([FromQuery] ReportFilterDto filterDto, [FromQuery] LazyLoadingDto lazyLoadingDto, CancellationToken cancellationToken)
    {
        var result = await reportService.LazyGetAllAsync(filterDto, lazyLoadingDto, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<ReportDto>>> LazyGetMine([FromQuery] ReportFilterDto filterDto, [FromQuery] LazyLoadingDto lazyLoadingDto, CancellationToken cancellationToken)
    {
        var result = await reportService.LazyGetByUserIdAsync(User.GetUserId(), filterDto, lazyLoadingDto, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("mine/stats")]
    public async Task<ActionResult<ReportHistoryStatsDto>> GetHistoryStats(CancellationToken cancellationToken)
    {
        var result = await reportService.GetHistoryStatsAsync(User.GetUserId(), cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("location")]
    public async Task<ActionResult<IReadOnlyList<ReportDto>>> LazyGetLocationReportsAsync(
        [FromQuery] Guid locationId,
        [FromQuery] ReportFilterDto filterDto,
        [FromQuery] LazyLoadingDto lazyLoadingDto, CancellationToken cancellationToken)
    {
        var result = await reportService.LazyGetLocationReportsAsync(locationId, filterDto, lazyLoadingDto, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("stats")]
    public async Task<ActionResult<MaintenanceStatsDto>> GetMaintenanceStats(CancellationToken cancellationToken)
    {
        var result = await reportService.GetMaintenanceStatsAsync(cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("table")]
    public async Task<ActionResult<IReadOnlyList<ReportDto>>> LazyGetTableReportsAsync(
        [FromQuery] ReportFilterDto filterDto,
        [FromQuery] LazyLoadingDto lazyDto,
        [FromQuery] bool allReports = false,
        CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<ReportDto>> result;

        if (allReports && User.IsAdmin())
            result = await reportService.LazyGetTableReportsAsync(null, filterDto, lazyDto, cancellationToken);
        else
            result = await reportService.LazyGetTableReportsAsync(User.GetUserId(), filterDto, lazyDto, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReportDto>> GetById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await reportService.GetByIdAsync(id, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost]
    [Transactional]
    public async Task<ActionResult<ReportCreateResponseDto>> CreateForLocation([FromForm] ReportCreateForLocationDto reportCreateForLocationDto, CancellationToken cancellationToken)
    {
        var result = await reportService.CreateForLocationIdAsync(User.GetUserId(), reportCreateForLocationDto, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost("coordinates")]
    [Transactional]
    public async Task<ActionResult<ReportCreateResponseDto>> CreateForCoordinates([FromForm] ReportCreateForCoordinatesDto reportCreateForCoordinatesDto, CancellationToken cancellationToken)
    {
        var result = await reportService.CreateForCoordinates(User.GetUserId(), reportCreateForCoordinatesDto, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPut("state")]
    [Authorize(Roles = nameof(AppRole.Admin))]
    [Transactional]
    public async Task<ActionResult<ReportDto>> UpdateState([FromBody] ReportUpdateStateDto reportUpdateStateDto, CancellationToken cancellationToken)
    {
        var result = await reportService.UpdateStateAsync(reportUpdateStateDto, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPut("rate")]
    [Transactional]
    public async Task<ActionResult<ReportDto>> Rate([FromBody] ReportRateDto reportRateDto, CancellationToken cancellationToken)
    {
        var result = await reportService.RateAsync(User.GetUserId(), reportRateDto, cancellationToken);

        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = nameof(AppRole.Admin))]
    [Transactional]
    public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await reportService.DeleteAsync(id, cancellationToken);

        return result.ToActionResult();
    }
}