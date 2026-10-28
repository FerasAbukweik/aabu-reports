using System.ComponentModel.DataAnnotations;

namespace AabuReports.Core.Dtos.Auth;

public class RefreshTokenRequestDto
{
    [Required]
    [StringLength(200)]
    public required string RefreshToken { get; set; }
}
