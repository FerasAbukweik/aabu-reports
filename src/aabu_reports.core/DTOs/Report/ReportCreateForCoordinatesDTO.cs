using System.ComponentModel.DataAnnotations;
using aabu_reports.core.Classes;

namespace aabu_reports.core.DTOs.Report;

public class ReportCreateForCoordinatesDTO : ReportCreate
{

    [Required]
    public required MapCoordinates Coordinates { get; set; }
}