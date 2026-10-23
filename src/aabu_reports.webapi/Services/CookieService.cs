using aabu_reports.core.DTOs.Account;
using aabu_reports.core.DTOs.Auth;
using aabu_reports.core.Interfaces.ServiceContracts;
using aabu_reports.webapi.Settings;
using Microsoft.Extensions.Options;

namespace aabu_reports.webapi.Services;

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

        // the path and domain must match the ones used when the cookie was set
        HttpContext.Response.Cookies.Delete(settings.AccessTokenCookieName, BuildOptions(AccessTokenPath, null));
        HttpContext.Response.Cookies.Delete(settings.RefreshTokenCookieName, BuildOptions(settings.RefreshTokenPath, null));
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
