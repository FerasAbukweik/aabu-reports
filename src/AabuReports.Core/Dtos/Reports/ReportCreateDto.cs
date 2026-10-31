using System.ComponentModel.DataAnnotations;
using AabuReports.Core.Enums;
using AabuReports.Core.Validation;
using Microsoft.AspNetCore.Http;

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
    [StringLength(500, MinimumLength = 1)]
    public required string Description { get; set; }

    /// <summary> optional images of the problem (sent as multipart/form-data, only image files are accepted) </summary>
    [ImageUploadLimits(maxCount: 5, maxFileSizeMb: 5)]
    public List<IFormFile> Images { get; set; } = [];
}
