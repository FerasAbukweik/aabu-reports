using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Results;
using AabuReports.Infrastructure.Logging;
using AabuReports.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace AabuReports.Infrastructure.Decorators.Logging;

/// <summary> logs the failed results of <see cref="RefreshTokenService"/> (a 4xx here is an invalid token, which is not worth a log) </summary>
public class LoggingRefreshTokenService(
    IRefreshTokenService innerService,
    ILogger<RefreshTokenService> logger) : IRefreshTokenService
{
    private const string Name = nameof(RefreshTokenService);

    public Task<Result<AabuReports.Core.Entities.RefreshToken>> GenerateRefreshTokenAsync(Guid userId, CancellationToken cancellationToken = default)
        => innerService.GenerateRefreshTokenAsync(userId, cancellationToken).LogFailure(logger, Name);

    public Task<Result<AabuReports.Core.Entities.RefreshToken>> ConsumeRefreshTokenAsync(string refreshTokenString, CancellationToken cancellationToken = default)
        => innerService.ConsumeRefreshTokenAsync(refreshTokenString, cancellationToken).LogFailure(logger, Name);

    public Task<Result> RevokeRefreshTokenAsync(string refreshTokenString, CancellationToken cancellationToken = default)
        => innerService.RevokeRefreshTokenAsync(refreshTokenString, cancellationToken).LogFailure(logger, Name);
}
