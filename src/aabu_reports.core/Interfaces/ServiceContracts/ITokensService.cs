using aabu_reports.core.Common;
using aabu_reports.core.DTOs.Account;
using aabu_reports.core.DTOs.Auth;
using aabu_reports.core.Entities.Identity;

namespace aabu_reports.core.Interfaces.ServiceContracts;

public interface ITokensService
{
    /// <summary>
    /// generates a new access token and a new refresh token (saved to the DB) for the user
    /// </summary>
    Task<Result<AuthResponseDto>> IssueTokensAsync(ApplicationUser user, CancellationToken cancellationToken = default);
}
