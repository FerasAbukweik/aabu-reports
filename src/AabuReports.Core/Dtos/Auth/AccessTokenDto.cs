namespace AabuReports.Core.Dtos.Auth;

public class AccessTokenDto
{
    public required string AccessToken { get; set; }
    public required DateTime ExpiresAt { get; set; }
}