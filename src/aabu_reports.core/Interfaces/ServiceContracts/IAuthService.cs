using aabu_reports.core.Common;
using aabu_reports.core.DTOs.Account;
using aabu_reports.core.DTOs.Auth;

namespace aabu_reports.core.Interfaces.ServiceContracts;

public interface IAuthService
{
    /// <summary>
    /// checks the credentials and, if valid, writes the auth cookies
    /// </summary>
    Task<Result> Login(LoginDto loginDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// reads the refresh token from the request cookies, consumes it (rotation) and writes a new token pair
    /// to the auth cookies. clears the cookies if it fails
    /// </summary>
    Task<Result> Refresh(CancellationToken cancellationToken = default);

    /// <summary>
    /// revokes the refresh token from the request cookies (if any) and removes both auth cookies,
    /// succeeds even if there is no token or it doesnt exist
    /// </summary>
    Task<Result> Logout(CancellationToken cancellationToken = default);
}
