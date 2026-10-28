using System.ComponentModel.DataAnnotations;
using aabu_reports.core.Enums;

namespace aabu_reports.core.DTOs.Report;

public class ReportCreate
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