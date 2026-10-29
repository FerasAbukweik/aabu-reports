using System.ComponentModel.DataAnnotations;

namespace AabuReports.Core.Dtos.Reports;

public class ReportCreateForLocationDto : ReportCreateDto
{
    /// <summary> the building / pin the report is about </summary>
    [Required]
    public required Guid LocationId { get; set; }
}
