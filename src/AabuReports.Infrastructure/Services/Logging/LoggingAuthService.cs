using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Results;
using AabuReports.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AabuReports.Core.Dtos.Auth;

namespace AabuReports.Infrastructure.Services.Logging;

/// <summary> logs the failed results of <see cref="AuthService"/> </summary>
public class LoggingAuthService(
    [FromKeyedServices("inner")] IAuthService innerService,
    ILogger<AuthService> logger) : IAuthService
{
    private const string Name = nameof(AuthService);

    // Login keeps its own warnings inside AuthService: they tell "locked out" and "wrong password" apart, and the result can't
    public Task<Result> Login(LoginDto loginDto, CancellationToken cancellationToken = default)
        => innerService.Login(loginDto, cancellationToken).LogFailure(logger, Name);

    public Task<Result> VerifyEmail(string studentId, string code, CancellationToken cancellationToken = default)
        => innerService.VerifyEmail(studentId, code, cancellationToken).LogFailure(logger, Name);

    public Task<Result> Refresh(CancellationToken cancellationToken = default)
        => innerService.Refresh(cancellationToken).LogFailure(logger, Name, logClientErrors: true);

    public Task<Result> Logout(CancellationToken cancellationToken = default)
        => innerService.Logout(cancellationToken).LogFailure(logger, Name);
}
