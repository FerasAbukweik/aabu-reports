using System.ComponentModel.DataAnnotations;
using AabuReports.Core.Enums;

namespace AabuReports.Core.Dtos.Reports;

public class ReportCreateDto
{
    [Required]
    [EnumDataType(typeof(ReportCategory))]
    public required ReportCategory Category { get; set; }

    [Required]
    [EnumDataType(typeof(ReportSeverity))]
    public required ReportSeverity Severity { get; set; }

    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public required string Description { get; set; }
}