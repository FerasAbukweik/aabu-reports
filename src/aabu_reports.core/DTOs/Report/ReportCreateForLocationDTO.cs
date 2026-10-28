using System.ComponentModel.DataAnnotations;

namespace aabu_reports.core.DTOs.Report;

public class ReportCreateForLocationDTO : ReportCreate
{
    /// <summary> the building / pin the report is about </summary>
    [Required]
    public required Guid LocationId { get; set; }
}
