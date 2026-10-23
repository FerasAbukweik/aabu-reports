using aabu_reports.core.Common;
using aabu_reports.core.Entities.Identity;

namespace aabu_reports.core.Interfaces.ServiceContracts;

public interface IAccessTokenService
{
    Task<Result<string>> GenerateAccessTokenAsync(ApplicationUser user, CancellationToken cancellationToken = default);
}