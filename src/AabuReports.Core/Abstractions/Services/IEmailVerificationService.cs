using AabuReports.Core.Entities.Identity;
using AabuReports.Core.Results;

namespace AabuReports.Core.Abstractions.Services;

public interface IEmailVerificationService
{
    Task<Result> BeginAsync(ApplicationUser user, CancellationToken cancellationToken = default);
    Task<Result> ResendAsync(string studentId, CancellationToken cancellationToken = default);
    Task<Result<ApplicationUser>> VerifyAsync(string studentId, string code, CancellationToken cancellationToken = default);
}
