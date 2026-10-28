using System.ComponentModel.DataAnnotations;

namespace AabuReports.Core.Dtos.Reports;

public class ReportRateDto
{
    [Required]
    public required Guid Id { get; set; }

    [Required]
    [Range(1, 5)]
    public required int Rate { get; set; }
}
