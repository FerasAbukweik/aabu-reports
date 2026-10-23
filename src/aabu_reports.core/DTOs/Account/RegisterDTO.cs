using System.ComponentModel.DataAnnotations;
using aabu_reports.core.Constants;

namespace aabu_reports.core.DTOs.Account;

public class RegisterDto
{
    [Required]
    [RegularExpression(pattern: RegularExpression.StudentId, ErrorMessage = "Student id must be exactly 10 digits")]
    public required string StudentId { get; set; }

    [Required]
    [StringLength(40, MinimumLength = 8)]
    public required string Password { get; set; }

    [Required]
    [StringLength(150)]
    [RegularExpression(pattern: RegularExpression.Email)]
    public required string Email { get; set; }

    [Required]
    [StringLength(150, MinimumLength = 2)]
    public required string FullName { get; set; }
}
