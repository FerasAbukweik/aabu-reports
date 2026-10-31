using AabuReports.Core.Dtos.Auth;

namespace AabuReports.Core.Abstractions.Services;

public interface ICookieService
{
    /// <summary>
    /// writes the access and refresh tokens to the response as HttpOnly cookies
    /// </summary>
    void SetAuthCookies(AuthResponseDto auth);

    /// <summary>
    /// removes both auth cookies
    /// </summary>
    void ClearAuthCookies();

    /// <returns> the refresh token from the request cookies, or null if there is none </returns>
    string? GetRefreshToken();

}
