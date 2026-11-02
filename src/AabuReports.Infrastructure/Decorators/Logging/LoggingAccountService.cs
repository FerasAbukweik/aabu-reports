using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.Accounts;
using AabuReports.Core.Results;
using AabuReports.Infrastructure.Logging;
using AabuReports.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace AabuReports.Infrastructure.Decorators.Logging;

/// <summary> logs the failed results of <see cref="AccountService"/> </summary>
public class LoggingAccountService(
    IAccountService innerService,
    ILogger<AccountService> logger) : IAccountService
{
    private const string Name = nameof(AccountService);

    public Task<Result> Register(RegisterDto registerDto, CancellationToken cancellationToken = default)
        => innerService.Register(registerDto, cancellationToken).LogFailure(logger, Name);
}
