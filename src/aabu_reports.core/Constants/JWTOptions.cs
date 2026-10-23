namespace aabu_reports.core.Constants;

public class JwtOptions
{
    public int RefreshTokenLifeTimeInSeconds { get; set; } = 0;
    public int AccessTokenLifeTimeInSeconds { get; set; } = 0;
    public string Key { get; set; } = "";
    public string Audience  { get; set; } = "";
    public string Issuer  { get; set; } = "";
}