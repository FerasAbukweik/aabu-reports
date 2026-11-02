using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Results;
using AabuReports.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AabuReports.Core.Dtos.Auth;
using AabuReports.Core.Entities.Identity;

namespace AabuReports.Infrastructure.Services.Logging;

/// <summary> logs the failed results of <see cref="AccessTokenService"/>, every failure here is unexpected so 4xx are logged too </summary>
public class LoggingAccessTokenService(
    [FromKeyedServices("inner")] IAccessTokenService innerService,
    ILogger<AccessTokenService> logger) : IAccessTokenService
{
    public async Task<Result<AccessTokenDto>> GenerateAccessTokenAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        var result = await innerService.GenerateAccessTokenAsync(user, cancellationToken);
        if (!result.IsSuccess)
        {
            var level = result.StatusCode >= System.Net.HttpStatusCode.InternalServerError ? LogLevel.Error : LogLevel.Warning;
            logger.Log(level, "{ServiceName}.{MethodName} user with Id: {UserId} failed creating an access token: {Error}",
                nameof(AccessTokenService), nameof(GenerateAccessTokenAsync), user.Id.ToString(), result.ErrorMessage);
        }
        return result;
    }
}
