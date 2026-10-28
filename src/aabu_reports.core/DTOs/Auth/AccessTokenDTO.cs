namespace aabu_reports.core.DTOs.Auth;

public class AccessTokenDTO
{
    public required string AccessToken { get; set; }
    public required DateTime ExpiresAt { get; set; }
}