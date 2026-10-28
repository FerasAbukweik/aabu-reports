using aabu_reports.core.Common;
using aabu_reports.core.DTOs.Auth;
using aabu_reports.core.Entities.Identity;

namespace aabu_reports.core.Interfaces.ServiceContracts;

public interface IAccessTokenService
{
    Task<Result<AccessTokenDTO>> GenerateAccessTokenAsync(ApplicationUser user, CancellationToken cancellationToken = default);
}