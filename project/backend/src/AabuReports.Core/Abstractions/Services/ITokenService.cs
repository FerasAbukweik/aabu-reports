using AabuReports.Core.Results;
using AabuReports.Core.Dtos.Accounts;
using AabuReports.Core.Dtos.Auth;
using AabuReports.Core.Entities.Identity;

namespace AabuReports.Core.Abstractions.Services;

public interface ITokenService
{
    /// <summary>
    /// generates a new access token and a new refresh token (saved to the DB) for the user
    /// </summary>
    Task<Result<AuthResponseDto>> IssueTokensAsync(ApplicationUser user, CancellationToken cancellationToken = default);
}
