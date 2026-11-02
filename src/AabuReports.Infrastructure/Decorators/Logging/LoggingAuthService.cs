using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.Auth;
using AabuReports.Core.Results;
using AabuReports.Infrastructure.Logging;
using AabuReports.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace AabuReports.Infrastructure.Decorators.Logging;

/// <summary> logs the failed results of <see cref="AuthService"/> </summary>
public class LoggingAuthService(
    IAuthService innerService,
    ILogger<AuthService> logger) : IAuthService
{
    private const string Name = nameof(AuthService);

    // a failed login is always worth a warning (locked out / wrong password), the status code and the log details tell them apart
    public Task<Result> Login(LoginDto loginDto, CancellationToken cancellationToken = default)
        => innerService.Login(loginDto, cancellationToken).LogFailure(logger, Name, logClientErrors: true);

    public Task<Result> VerifyEmail(string studentId, string code, CancellationToken cancellationToken = default)
        => innerService.VerifyEmail(studentId, code, cancellationToken).LogFailure(logger, Name);

    public Task<Result> Refresh(CancellationToken cancellationToken = default)
        => innerService.Refresh(cancellationToken).LogFailure(logger, Name, logClientErrors: true);

    public Task<Result> Logout(CancellationToken cancellationToken = default)
        => innerService.Logout(cancellationToken).LogFailure(logger, Name);
}
