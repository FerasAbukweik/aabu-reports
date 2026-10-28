using System.ComponentModel.DataAnnotations;
using aabu_reports.core.Enums;

namespace aabu_reports.core.DTOs.Report;

public class ReportUpdateStateDTO
{
    [Required]
    public required Guid Id { get; set; }

    [Required]
    [EnumDataType(typeof(ReportState))]
    public required ReportState State { get; set; }
}
