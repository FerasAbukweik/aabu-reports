using System.ComponentModel.DataAnnotations;
using AabuReports.Core.Constants;

namespace AabuReports.Core.Dtos.Auth;

public class ResendCodeDto
{
    [Required, RegularExpression(RegexPatterns.StudentId)]
    public required string StudentId { get; set; }
}
