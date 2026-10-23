using System.ComponentModel.DataAnnotations;
using aabu_reports.core.Constants;

namespace aabu_reports.core.DTOs.Account;

public class LoginDto
{
    [Required]
    [RegularExpression(pattern: RegularExpression.StudentId, ErrorMessage = "Student id must be exactly 10 digits")]
    public required string StudentId { get; set; }

    [Required]
    [StringLength(100)]
    public required string Password { get; set; }
}
