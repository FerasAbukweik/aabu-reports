using System.ComponentModel.DataAnnotations;
using AabuReports.Core.ValueObjects;

namespace AabuReports.Core.Dtos.Reports;

public class ReportCreateForCoordinatesDto : ReportCreateDto
{

    [Required]
    public required MapCoordinates Coordinates { get; set; }
}