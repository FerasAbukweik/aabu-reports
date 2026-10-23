namespace aabu_reports.core.DTOs.Auth;

public class AuthResponseDto
{
    public required string AccessToken { get; set; }
    public required DateTime AccessTokenExpiresAt { get; set; }

    public required string RefreshToken { get; set; }
    public required DateTime RefreshTokenExpiresAt { get; set; }
}
