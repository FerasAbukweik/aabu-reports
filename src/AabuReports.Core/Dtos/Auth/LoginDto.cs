using System.ComponentModel.DataAnnotations;
using AabuReports.Core.Constants;

namespace AabuReports.Core.Dtos.Auth;

public class LoginDto
{
    [Required]
    [RegularExpression(pattern: RegexPatterns.StudentId, ErrorMessage = "Student id must be exactly 10 digits")]
    public required string StudentId { get; set; }

    [Required]
    [StringLength(100)]
    public required string Password { get; set; }
}
