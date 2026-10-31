using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.Auth;
using AabuReports.Webapi.Settings;
using Microsoft.Extensions.Options;

namespace AabuReports.Webapi.Services;

public class CookieService(
    IHttpContextAccessor httpContextAccessor,
    IOptions<AuthCookieSettings> options) : ICookieService
{
    private const string AccessTokenPath = "/";

    private HttpContext HttpContext => httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("There is no active HTTP context");

    public void SetAuthCookies(AuthResponseDto auth)
    {
        var settings = options.Value;
        // Append the expired legacy cookie so deleting the current cookie cannot discard this header.
        if (settings.RefreshTokenPath != "/api/auth/refresh")
            HttpContext.Response.Cookies.Append(settings.RefreshTokenCookieName, string.Empty,
                BuildOptions("/api/auth/refresh", DateTime.UnixEpoch));

        HttpContext.Response.Cookies.Append(
            settings.AccessTokenCookieName,
            auth.AccessToken,
            BuildOptions(AccessTokenPath, auth.AccessTokenExpiresAt));

        HttpContext.Response.Cookies.Append(
            settings.RefreshTokenCookieName,
            auth.RefreshToken,
            BuildOptions(settings.RefreshTokenPath, auth.RefreshTokenExpiresAt));
    }

    public void ClearAuthCookies()
    {
        var settings = options.Value;
        HttpContext.Response.Cookies.Delete(settings.AccessTokenCookieName, BuildOptions(AccessTokenPath, null));
        HttpContext.Response.Cookies.Delete(settings.RefreshTokenCookieName, BuildOptions(settings.RefreshTokenPath, null));
        if (settings.RefreshTokenPath != "/api/auth/refresh")
            HttpContext.Response.Cookies.Append(settings.RefreshTokenCookieName, string.Empty,
                BuildOptions("/api/auth/refresh", DateTime.UnixEpoch));
    }

    public string? GetRefreshToken()
    {
        return HttpContext.Request.Cookies[options.Value.RefreshTokenCookieName];
    }


    // private

    private CookieOptions BuildOptions(string path, DateTime? expiresAtUtc)
    {
        var settings = options.Value;

        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = settings.SameSite,
            Path = path,
            Domain = string.IsNullOrWhiteSpace(settings.Domain) ? null : settings.Domain,
            IsEssential = true,
            Expires = expiresAtUtc is null
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc.Value, DateTimeKind.Utc))
        };
    }
}
