using System.ComponentModel.DataAnnotations;
using AabuReports.Core.Constants;

namespace AabuReports.Core.Dtos.Auth;

public class VerifyEmailDto
{
    [Required, RegularExpression(RegexPatterns.StudentId)]
    public required string StudentId { get; set; }

    [Required, RegularExpression(@"^[0-9]{6}$")]
    public required string Code { get; set; }
}
