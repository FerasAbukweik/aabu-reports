using aabu_reports.core.Common;
using aabu_reports.core.DTOs.Account;

namespace aabu_reports.core.Interfaces.ServiceContracts;

public interface IAccountService
{
    Task<Result> Register(RegisterDto registerDto, CancellationToken cancellationToken = default);
}
