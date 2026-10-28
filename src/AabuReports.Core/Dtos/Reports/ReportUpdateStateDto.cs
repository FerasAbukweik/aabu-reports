using System.ComponentModel.DataAnnotations;
using AabuReports.Core.Enums;

namespace AabuReports.Core.Dtos.Reports;

public class ReportUpdateStateDto
{
    [Required]
    public required Guid Id { get; set; }

    [Required]
    [EnumDataType(typeof(ReportState))]
    public required ReportState State { get; set; }
}
