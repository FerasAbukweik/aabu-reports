using System.ComponentModel.DataAnnotations;

namespace aabu_reports.core.DTOs.Auth;

public class RefreshTokenRequestDto
{
    [Required]
    [StringLength(200)]
    public required string RefreshToken { get; set; }
}
