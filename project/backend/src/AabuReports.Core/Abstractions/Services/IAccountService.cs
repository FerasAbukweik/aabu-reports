using AabuReports.Core.Results;
using AabuReports.Core.Dtos.Accounts;

namespace AabuReports.Core.Abstractions.Services;

public interface IAccountService
{
    Task<Result> Register(RegisterDto registerDto, CancellationToken cancellationToken = default);
}
