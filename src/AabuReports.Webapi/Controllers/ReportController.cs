using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Core.Dtos.Reports;
using AabuReports.Core.Enums;
using AabuReports.Webapi.ExtensionMethods;
using AabuReports.Webapi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AabuReports.Webapi.Controllers;

[Authorize]
public class ReportController(IReportService reportService) : ApiControllerBase
{
    /// <summary>
    /// one page of the reports table: the current user's reports, or everyone's when an admin sends allReports=true
    /// (a user who is not an admin always gets only their own, the flag is ignored)
    /// </summary>
    [HttpGet("table")]
    public async Task<ActionResult<IReadOnlyList<ReportDto>>> LazyGetFilteredTableReports(
        [FromQuery] ReportFilterDto filterDto,
        [FromQuery] LazyLoadingDto lazyLoadingDto,
        [FromQuery] bool allReports = false,
        CancellationToken cancellationToken = default)
    {
        var result = await reportService.LazyGetFilteredAsync(TableScopeUserId(allReports), filterDto, lazyLoadingDto, cancellationToken);

        return result.ToActionResult();
    }

    /// <summary> the numbers of the table's stats cards, for the same reports the table shows (see <see cref="LazyGetFilteredTableReports"/>) </summary>
    [HttpGet("table/stats")]
    public async Task<ActionResult<ReportHistoryStatsDto>> GetTableStats([FromQuery] bool allReports = false, CancellationToken cancellationToken = default)
    {
        var result = await reportService.GetHistoryStatsAsync(TableScopeUserId(allReports), cancellationToken);

        return result.ToActionResult();
    }

    /// <summary> one page of the reports of a location, in the short shape the map needs </summary>
    [HttpGet("location")]
    public async Task<ActionResult<IReadOnlyList<ReportMapDto>>> LazyGetLocationReports(
        [FromQuery] Guid locationId,
        [FromQuery] LazyLoadingDto lazyLoadingDto,
        CancellationToken cancellationToken)
    {
        var result = await reportService.LazyGetMapReportsAsync(locationId, lazyLoadingDto, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("stats")]
    public async Task<ActionResult<MaintenanceStatsDto>> GetMaintenanceStats(CancellationToken cancellationToken)
    {
        var result = await reportService.GetMaintenanceStatsAsync(cancellationToken);

        return result.ToActionResult();
    }

    /// <summary>
    /// one report, by its guid or by its short number ("968FCB28" / "#968FCB28").
    /// Every authenticated user can view a report.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ReportDto>> GetById([FromRoute] string id, CancellationToken cancellationToken)
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

    // private

    /// <summary> null (everyone's reports) only for an admin who asked for them, otherwise the current user </summary>
    private Guid? TableScopeUserId(bool allReports) => allReports && User.IsAdmin() ? null : User.GetUserId();
}
