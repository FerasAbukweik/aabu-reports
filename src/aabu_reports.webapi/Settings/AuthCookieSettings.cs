using Microsoft.AspNetCore.Http;

namespace aabu_reports.webapi.Settings;

public class AuthCookieSettings
{
    // fixed random names (not generated at startup, so cookies survive restarts and multiple instances)
    public string AccessTokenCookieName { get; set; } = "Dmvohoe4cgwpWGHPRVWMuhsu";
    public string RefreshTokenCookieName { get; set; } = "EoYJEmbNULzMhzCkHrfP33dv";

    /// <summary>
    /// the refresh cookie is only sent to this path (refresh and logout endpoints)
    /// </summary>
    public string RefreshTokenPath { get; set; } = "/api/auth/refresh";

    /// <summary>
    /// Lax works when the frontend and the api are on the same site (e.g. localhost:4200 and localhost:5299,
    /// or app.example.com and api.example.com). Use None only if they are on different sites.
    /// </summary>
    public SameSiteMode SameSite { get; set; } = SameSiteMode.Strict;

    /// <summary>
    /// optional, leave empty to make the cookie host-only
    /// </summary>
    public string? Domain { get; set; }
}
