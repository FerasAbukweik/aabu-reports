using System.ComponentModel.DataAnnotations;

namespace aabu_reports.core.DTOs.Report;

public class ReportRateDTO
{
    [Required]
    public required Guid Id { get; set; }

    [Required]
    [Range(1, 5)]
    public required int Rate { get; set; }
}
