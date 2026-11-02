using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Entities.Identity;
using AabuReports.Core.Results;
using AabuReports.Infrastructure.Logging;
using AabuReports.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace AabuReports.Infrastructure.Decorators.Logging;

/// <summary> logs the failed results of <see cref="EmailVerificationService"/> </summary>
public class LoggingEmailVerificationService(
    IEmailVerificationService innerService,
    ILogger<EmailVerificationService> logger) : IEmailVerificationService
{
    private const string Name = nameof(EmailVerificationService);

    public Task<Result> BeginAsync(ApplicationUser user, CancellationToken cancellationToken = default)
        => innerService.BeginAsync(user, cancellationToken).LogFailure(logger, Name);

    public Task<Result> ResendAsync(string studentId, CancellationToken cancellationToken = default)
        => innerService.ResendAsync(studentId, cancellationToken).LogFailure(logger, Name);

    public Task<Result<ApplicationUser>> VerifyAsync(string studentId, string code, CancellationToken cancellationToken = default)
        => innerService.VerifyAsync(studentId, code, cancellationToken).LogFailure(logger, Name);
}
