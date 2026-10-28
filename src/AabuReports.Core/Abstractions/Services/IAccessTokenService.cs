using AabuReports.Core.Results;
using AabuReports.Core.Dtos.Auth;
using AabuReports.Core.Entities.Identity;

namespace AabuReports.Core.Abstractions.Services;

public interface IAccessTokenService
{
    Task<Result<AccessTokenDto>> GenerateAccessTokenAsync(ApplicationUser user, CancellationToken cancellationToken = default);
}